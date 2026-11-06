// Offline generator for grouped editors. Every generated editor remains explicit XAML/C#.
// Core group settings, operation enums and action projection are owned by the core generator.
const path = require('path');
module.exports = ({groups, specs, core, ui, write}) => {
 const byOperation = new Map(specs.map(s => [s.op, s]));
 const groupByOperation = new Map(groups.flatMap(group => group.operations.map(op => [op, group])));
 const quote = value => JSON.stringify(value);
 const type = field => ['expression', 'variable'].includes(field.type) ? 'string' : field.type;
 const xml = value => String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;').replace(/\n/g, '&#10;');
 const numeric = field => ['int', 'double'].includes(field.type);
 const expression = field => field.type === 'expression';
 const info = (field, action) => {
  let text = field.info || '';
  if (field.name === 'LocalPath') text = ['FoeRead', 'BoardFileRead'].includes(action.op)
   ? '本地保存目标文件路径表达式；将本次从设备读取的内容保存到此路径，不是设备端文件路径。'
   : '本地已有源文件路径表达式；本次把此文件内容写入或更新到设备，不是本地保存目标，也不是设备端路径。';
  return text + (field.type === 'int' && !field.choices ? ' 整数支持十进制和 0x 十六进制。' : '')
   + (field.min !== undefined && field.max !== undefined ? ` 范围：${field.min}～${field.max}。` : '');
 };
 const operationReference = op => {
  const owner = groupByOperation.get(op), action = byOperation.get(op);
  if (!owner || !action) return op;
  return owner.operations.length === 1 ? `${owner.title}（ZlgEtherCat.${owner.name}）`
   : `${owner.title} / ${action.title}（ZlgEtherCat.${owner.name} / ${op}）`;
 };
 const help = action => action.help.replace(/ZlgEtherCat\.([A-Za-z0-9]+)/g, (match, op) => {
  const owner = groupByOperation.get(op);
  return owner ? operationReference(op) : match;
 });
 for (const group of groups) {
  const name = group.name, enumType = name + 'Operation', settingType = name + 'Setting';
  const actions = group.operations.map(op => {
   const action = byOperation.get(op);
   if (!action) throw new Error(`Unknown operation ${op} in group ${name}`);
   return action;
  });
  const fields = group.fields.filter(f => f.name !== 'Operation');
  const operationSelector = actions.length > 1 ? `                    <TextBlock Text="功能" FontWeight="SemiBold" Margin="0,4,0,4" />
                    <ComboBox x:Name="OperationEditor" ItemsSource="{Binding OperationChoices}" SelectedValuePath="Key" DisplayMemberPath="Value" SelectedValue="{Binding Operation, Mode=TwoWay}" Margin="0,0,0,8" />
                    <TextBlock Text="切换功能保留本组已填参数；只显示、校验和执行当前功能的参数。输入/输出方向随当前功能说明确定。" TextWrapping="Wrap" Opacity="0.7" Margin="0,0,0,12" />
` : '';
  const icon = group.icon || 'zlg_' + group.operations[0].toLowerCase() + '.png';
  const dynamicText = (field, select) => actions.filter(s => s.fields.some(f => f.name === field.name))
   .map(s => `        ${enumType}.${s.op} => ${quote(select(s.fields.find(f => f.name === field.name), s))},`).join('\n');
  const properties = fields.map(field => {
   const fieldType = type(field), fieldName = field.name;
   const choices = field.choices ? `\n    public IReadOnlyList<KeyValuePair<${fieldType}, string>> ${fieldName}Choices => [${field.choices.map(c => `new(${numeric(field) ? c.value : fieldType + '.' + c.value}, ${quote(c.label)})`).join(', ')}];` : '';
   return `    public ${fieldType} ${fieldName}
    {
        get => Model.${fieldName};
        set { if (Equals(Model.${fieldName}, value)) return; Model.${fieldName} = value; ${numeric(field) ? `NumericValueChanged(nameof(${fieldName})); ` : ''}Changed(); }
    }${numeric(field) && !field.choices ? `\n    public string ${fieldName}Text { get => GetNumericText(nameof(${fieldName}), Model.${fieldName}); set => Set${field.type === 'int' ? 'Integer' : 'Double'}Text(nameof(${fieldName}), value, parsed => ${fieldName} = parsed); }` : ''}${choices}
    public bool Is${fieldName}Active => Model.ActiveFields.Contains(nameof(${settingType}.${fieldName}));
    public Visibility ${fieldName}Visibility => Is${fieldName}Active ? Visibility.Visible : Visibility.Collapsed;
    public string ${fieldName}Label => Operation switch
    {
${dynamicText(field, f => f.label)}
        _ => ""
    };
    public string ${fieldName}Info => Operation switch
    {
${dynamicText(field, info)}
        _ => ""
    };`;
  }).join('\n\n');
  write(path.join(ui, 'ViewModels', name + 'ViewModel.cs'), `using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ${name}ViewModel : ZlgViewModelBase<${settingType}>
{
    public ${enumType} Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<${enumType}, string>> OperationChoices => [${actions.map(s => `new(${enumType}.${s.op}, ${quote(s.title)})`).join(', ')}];
    public string OperationTitle => Operation switch
    {
${actions.map(s => `        ${enumType}.${s.op} => ${quote(s.title)},`).join('\n')}
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
${actions.map(s => `        ${enumType}.${s.op} => ${quote(help(s))},`).join('\n')}
        _ => "当前功能未定义，不能执行。"
    };
${properties}
}
`);

  const controls = fields.map(field => {
   const n = field.name;
   const control = expression(field) ? `<expr:ExpressionTextBox x:Name="${n}Editor" ScriptText="{Binding ${n}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" ${(!field.evalType || field.evalType === 'string') ? 'ExpectedResultType="String"' : ''}
                            SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
                            EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />`
    : field.type === 'bool' ? `<CheckBox x:Name="${n}Editor" IsChecked="{Binding ${n}, Mode=TwoWay}" Content="{Binding ${n}Label}" />`
    : field.choices ? `<ComboBox x:Name="${n}Editor" ItemsSource="{Binding ${n}Choices}" SelectedValuePath="Key" DisplayMemberPath="Value" SelectedValue="{Binding ${n}, Mode=TwoWay}" />`
    : `<TextBox x:Name="${n}Editor" ${field.type === 'variable' ? 'common:VariableDrop.Target="True"' : ''} Text="{Binding ${n}${numeric(field) ? 'Text' : ''}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, ValidatesOnExceptions=True}" />`;
   return `                    <StackPanel Visibility="{Binding ${n}Visibility}" Margin="0,0,0,12">
                        <TextBlock Text="{Binding ${n}Label}" FontWeight="SemiBold" Margin="0,4,0,4" />
                        ${control}
                        <TextBlock Text="{Binding ${n}Info}" TextWrapping="Wrap" Opacity="0.7" />
                    </StackPanel>`;
  }).join('\n');
  write(path.join(ui, 'Views', name + 'EditorView.xaml'), `<UserControl x:Class="ZlgEtherCat.UI.Views.${name}EditorView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
    xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
    xmlns:common="clr-namespace:PluginCommon.UI" common:PluginEditorScrolling.Enabled="True">
    <syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
        <syncfusion:TabItemExt Header="${xml(group.title)}" Image="pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/${icon}" ImageHeight="20" ImageWidth="20">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="16">
${operationSelector}                    <TextBlock Text="{Binding OperationTitle}" FontWeight="SemiBold" Margin="0,0,0,8" />
                    <TextBlock Text="{Binding OperationHelp}" TextWrapping="Wrap" Margin="0,0,0,12" />
                    <TextBlock Text="{Binding Error}" Foreground="Firebrick" TextWrapping="Wrap" />
${controls}
                </StackPanel>
            </ScrollViewer>
        </syncfusion:TabItemExt>
    </syncfusion:TabControlExt>
</UserControl>
`);

  const commits = fields.map(field => {
   const dependency = expression(field) ? 'ExpressionTextBox.ExpressionTextBox.ScriptTextProperty'
    : field.type === 'bool' ? 'System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty'
    : field.choices ? 'System.Windows.Controls.Primitives.Selector.SelectedValueProperty' : 'TextBox.TextProperty';
   return `        if (ViewModel.Is${field.name}Active) ${field.name}Editor.GetBindingExpression(${dependency})?.UpdateSource();`;
  }).join('\n');
  write(path.join(ui, 'Views', name + 'EditorView.xaml.cs'), `using System.Windows;
using System.Windows.Controls;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.ViewModels;

namespace ZlgEtherCat.UI.Views;

public partial class ${name}EditorView : UserControl, IRefreshableEditor
{
    public ${name}ViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(${name}EditorView), new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(${name}EditorView), new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public ${name}EditorView()
    {
        InitializeComponent();
        ViewModel = new ${name}ViewModel();
        DataContext = ViewModel;
        ViewModel.CommitPendingInput = CommitPendingInputs;
        PluginCommon.UI.PluginEditorCommit.Attach(this, CommitAndFlush);
        Unloaded += (_, _) => CommitAndFlush();
    }
    private void CommitPendingInputs()
    {
${commits}
    }
    private void CommitAndFlush()
    {
        CommitPendingInputs();
        ViewModel.Flush();
    }
    public void RefreshFromStep(Step step)
    {
        CommitAndFlush();
        ViewModel.AttachSerializer(new ${name}Plugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
`);

  const branches = actions.map(action => {
   const expressionChecks = action.fields.filter(expression).map(f => `                    ValidateExpression(s.${f.name}, ${quote(f.label)}, context, errors);`).join('\n');
   const variableChecks = action.fields.filter(f => f.type === 'variable').map(f => `                    ValidateVariable(s.${f.name}, ${f.name === 'DataVariable'}, ${quote(f.label)}, context, errors);`).join('\n');
   const preceding = action.prerequisites.map(op => `                    ValidatePreceding(context, "ZlgEtherCat.${op}", ${action.needsConnection ? 's.ConnectionExpression' : '""'}, errors, ${op.includes('Pool') || ['BindAxis', 'SetAxisMode'].includes(op) ? 's.ResourceExpression' : '""'}, ${quote(operationReference(op))});`).join('\n');
   return `                case ${enumType}.${action.op}:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.${action.op}Setting)grouped.CreateOperationSetting();
${expressionChecks}
${variableChecks}
${preceding}
                    break;
                }`;
  }).join('\n');
  write(path.join(ui, 'Editors', name + 'EditorPlugin.cs'), `using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;

namespace ZlgEtherCat.UI.Editors;

public sealed class ${name}EditorPlugin : ZlgEditorPluginBase<${settingType}>
{
    public override string StepTypeId => new ${name}Plugin().StepTypeId;
    public override string IconPath => new ${name}Plugin().IconPath;
    public override FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ${name}EditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new ${name}Plugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public override Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var grouped = (${settingType})new ${name}Plugin().CreateSerializer().Deserialize(context.Setting, 1);
            errors.AddRange(grouped.Validate().Select(error => StepSettingError.Error("ZEC_001", error)));
            switch (grouped.Operation)
            {
${branches}
                default: errors.Add(StepSettingError.Error("ZEC_OPERATION", "请选择当前分组支持的功能")); break;
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("ZEC_SETTING", $"读取或校验步骤配置失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
`);
 }
 console.log(`Generated ${groups.length} explicit grouped editors.`);
};
