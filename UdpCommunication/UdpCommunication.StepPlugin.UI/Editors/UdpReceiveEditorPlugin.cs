using System.Net;
using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using UdpCommunication.UI.Views;
using UdpCommunication.Validation;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Editors;

public sealed class UdpReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Communication.UdpReceive";
    public string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpReceiveEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new UdpReceivePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var errors = new List<StepSettingError>();
            var serializer = new UdpReceivePlugin().CreateSerializer();
            UdpReceiveSetting s;
            if (context.Setting is { Length: > 0 })
            {
                s = (UdpReceiveSetting)serializer.Deserialize(context.Setting, 1);
            }
            else
            {
                s = (UdpReceiveSetting)serializer.CreateDefault();
            }

            UdpOpenStepAddressValidator.ValidateOpenStepAddress(
                s.OpenStepAddress, context.SequenceFile, errors);

            if (s.ReceiveTimeoutMs <= 0)
            {
                errors.Add(StepSettingError.Error("UDP_004", "接收超时必须大于 0"));
            }

            if (!string.IsNullOrEmpty(s.ExpectedReply))
            {
                try
                {
                    UdpMessageCodec.Encode(s.ExpectedReply, s.ReplyFormat);
                }
                catch (Exception)
                {
                    errors.Add(StepSettingError.Error("UDP_005", "期望回复报文格式无效"));
                }

                if (!context.Evaluator.ValidateExpression(s.ExpectedReply, context.ExecutionContext, out var expectedErr))
                {
                    errors.Add(StepSettingError.Error("UDP_005E", $"ExpectedReply 表达式无效：{expectedErr}"));
                }
            }

            var responseVariableError = UdpResponseVariable.Validate(s.ResponseVariable, context.ExecutionContext);
            if (responseVariableError is not null)
            {
                errors.Add(StepSettingError.Error("UDP_006", responseVariableError));
            }

            return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
        }
        catch (Exception ex)
        {
            return Task.FromResult<IReadOnlyList<StepSettingError>>(
                [StepSettingError.Error("UDP_000", $"UDP 配置无法读取：{ex.Message}")]);
        }
    }
}
