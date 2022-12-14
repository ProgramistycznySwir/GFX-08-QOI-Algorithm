namespace QOI_Algorithm;

public static class Prelude
{
    public static void PANIC(string errorMessage)
    {
        Console.WriteLine($"ERROR: {errorMessage}");
        System.Environment.Exit(1);
    }
	public static NotImplementedException TODO => TODO_();
    public static NotImplementedException TODO_(string message= null) => new NotImplementedException(message);
}