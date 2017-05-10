using System;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.Testing;

using Ninject.Syntax;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
[SetUpFixture]
public class AssemblyInitialize
{
    
    public static IResolutionRoot ResolutionRoot { get; private set; }

    private TestingKernel<ConsoleLog> kernel;

    [OneTimeSetUp]
    public void SetUp()
    {
        try
        {
            kernel = new TestingKernel<ConsoleLog>();            
            ResolutionRoot = kernel;
        }
        catch(Exception ex)
        {
            Console.Write(ex);
            throw;
        }
        
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        kernel.Dispose();
    }
}