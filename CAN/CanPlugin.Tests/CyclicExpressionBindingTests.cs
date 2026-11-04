using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.Models;
using CAN.UI.Converters;
using Xunit;

namespace CAN.Tests;

public sealed class CyclicExpressionBindingTests
{
    [StaFact]
    public void Period_binding_displays_literal_and_writes_only_canonical_expression()
    {
        var row = new CyclicMessageItem { CycleTimeMs = 80 };
        var box = Bind(row, nameof(row.CycleTimeMsExpression), nameof(row.CycleTimeMs));
        Assert.Equal("80", box.Text);
        row.CycleTimeMs = 90;
        Assert.Equal("90", box.Text);
        Edit(box, "Locals.PeriodMs");
        Assert.Equal("Locals.PeriodMs", row.CycleTimeMsExpression);
        Assert.Equal(90, row.CycleTimeMs);
        row.CycleTimeMsExpression = "Locals.NewPeriod";
        Assert.Equal("Locals.NewPeriod", box.Text);
        row.CycleTimeMsExpression = "";
        Assert.Equal("90", box.Text);
    }

    [StaFact]
    public void Enabled_binding_preserves_literal_and_follows_rebound_row()
    {
        var old = new CyclicMessageItem { Enabled = false };
        var current = new CyclicMessageItem { Enabled = true };
        var box = Bind(old, nameof(old.EnabledExpression), nameof(old.Enabled));
        Assert.Equal("false", box.Text);
        Edit(box, "Locals.SendEnabled");
        Assert.Equal("Locals.SendEnabled", old.EnabledExpression);
        Assert.False(old.Enabled);
        box.DataContext = current;
        Assert.Equal("true", box.Text);
        old.EnabledExpression = "Locals.Old";
        Assert.Equal("true", box.Text);
        Edit(box, "false");
        Assert.Equal("false", current.EnabledExpression);
        Assert.True(current.Enabled);
        Assert.Equal("Locals.Old", old.EnabledExpression);
    }

    private static TextBox Bind(CyclicMessageItem row, string expression, string literal)
    {
        var box = new TextBox { DataContext = row };
        var binding = new MultiBinding
        {
            Converter = new CyclicExpressionTextConverter(), Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        binding.Bindings.Add(new Binding(expression) { Mode = BindingMode.TwoWay });
        binding.Bindings.Add(new Binding(literal) { Mode = BindingMode.OneWay });
        BindingOperations.SetBinding(box, TextBox.TextProperty, binding);
        return box;
    }

    private static void Edit(TextBox box, string text)
    {
        box.SetCurrentValue(TextBox.TextProperty, text);
        BindingOperations.GetMultiBindingExpression(box, TextBox.TextProperty)!.UpdateSource();
    }
}
