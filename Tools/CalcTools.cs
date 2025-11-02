//https://github.com/virex-84

using System.ComponentModel;
using ModelContextProtocol.Server;

/// <summary>
/// Tools for calculations.
/// </summary>
public class CalcTools
{
    [McpServerTool, Description("Adds two numbers and returns the result.")]
    public static double Add(
        [Description("First number")] double a,
        [Description("Second number")] double b)
    {
        return a + b;
    }

    [McpServerTool, Description("Subtracts the second number from the first and returns the result.")]
    public static double Subtract(
        [Description("First number")] double a,
        [Description("Second number")] double b)
    {
        return a - b;
    }

    [McpServerTool, Description("Multiplies two numbers and returns the result.")]
    public static double Multiply(
        [Description("First number")] double a,
        [Description("Second number")] double b)
    {
        return a * b;
    }

    [McpServerTool, Description("Divides the first number by the second and returns the result.")]
    public static double Divide(
        [Description("Dividend (number to be divided)")] double a,
        [Description("Divisor (number to divide by)")] double b)
    {
        if (b == 0)
            throw new ArgumentException("Cannot divide by zero");

        return a / b;
    }

    [McpServerTool, Description("Calculates the power of a number.")]
    public static double Power(
        [Description("Base number")] double baseNumber,
        [Description("Exponent")] double exponent)
    {
        return Math.Pow(baseNumber, exponent);
    }

    [McpServerTool, Description("Calculates the square root of a number.")]
    public static double SquareRoot([Description("Number to calculate square root of")] double number)
    {
        if (number < 0)
            throw new ArgumentException("Cannot calculate square root of negative number");

        return Math.Sqrt(number);
    }

}