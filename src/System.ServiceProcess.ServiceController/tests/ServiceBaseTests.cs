// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Security.Principal;
using Xunit;

/// <summary>
/// NOTE: All tests checking the output file should always call Stop before checking because Stop will flush the file to disk.
/// </summary>
namespace System.ServiceProcess.Tests
{
    [OuterLoop(/* Modifies machine state */)]
    public class ServiceBaseTests : IDisposable
    {
        private readonly TestServiceProvider _testService;

        public ServiceBaseTests()
        {
            _testService = new TestServiceProvider();
        }

        private static bool RunningWithElevatedPrivileges
        {
            get { return TestServiceProvider.RunningWithElevatedPrivileges; }
        }
        private void AssertExpectedProperties(ServiceController testServiceController)
        {
            var comparer = PlatformDetection.IsFullFramework ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal; // Full framework upper cases the name
            Assert.Equal(_testService.TestServiceName, testServiceController.ServiceName, comparer);
            Assert.Equal(_testService.TestServiceDisplayName, testServiceController.DisplayName);
            Assert.Equal(_testService.TestMachineName, testServiceController.MachineName);
            Assert.Equal(ServiceType.Win32OwnProcess, testServiceController.ServiceType);
            Assert.True(testServiceController.CanPauseAndContinue);
            Assert.True(testServiceController.CanStop);
            Assert.True(testServiceController.CanShutdown);
        }

        //[Fact]
        // To cleanup lingering Test Services uncomment the Fact attribute and run the following command
        //   msbuild /t:rebuildandtest /p:XunitMethodName=System.ServiceProcess.Tests.ServiceBaseTests.Cleanup
        // Remember to comment out the Fact again before running tests otherwise it will cleanup tests running in parallel
        // and casue them to fail.
        public void Cleanup()
        {
            string currentService = "";
            foreach (ServiceController controller in ServiceController.GetServices())
            {
                try
                {
                    currentService = controller.DisplayName;
                    if (controller.DisplayName.StartsWith("Test Service"))
                    {
                        Console.WriteLine("Trying to clean-up " + currentService);
                        TestServiceInstaller deleteService = new TestServiceInstaller()
                        {
                            ServiceName = controller.ServiceName
                        };
                        deleteService.RemoveService();
                        Console.WriteLine("Cleaned up " + currentService);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed " + ex.Message);
                }
            }
        }

        [ConditionalFact(nameof(RunningWithElevatedPrivileges))]
        public void TestOnStartThenStop()
        {
            var controller = new ServiceController(_testService.TestServiceName);
            AssertExpectedProperties(controller);
            string expected =
@"OnStart args=
OnStop
";
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
            Assert.Equal(expected, _testService.GetServiceOutput());
        }

        [ConditionalFact(nameof(RunningWithElevatedPrivileges))]
        public void TestOnStartWithArgsThenStop()
        {
            var controller = new ServiceController(_testService.TestServiceName);
            AssertExpectedProperties(controller);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);

            string expected =
@"OnStart args=a,b,c
OnStop
";
            controller.Start(new string[] { "a", "b", "c" });
            controller.WaitForStatus(ServiceControllerStatus.Running);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
            Assert.Equal(expected, _testService.GetServiceOutput());
        }

        [ConditionalFact(nameof(RunningWithElevatedPrivileges))]
        public void TestOnPauseThenStop()
        {
            var controller = new ServiceController(_testService.TestServiceName);
            AssertExpectedProperties(controller);
            string expected =
@"OnStart args=
OnPause
OnStop
";
            controller.Pause();
            controller.WaitForStatus(ServiceControllerStatus.Paused);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
            Assert.Equal(expected, _testService.GetServiceOutput());
        }

        [ConditionalFact(nameof(RunningWithElevatedPrivileges))]
        public void TestOnPauseAndContinueThenStop()
        {
            var controller = new ServiceController(_testService.TestServiceName);
            AssertExpectedProperties(controller);
            string expected =
@"OnStart args=
OnPause
OnContinue
OnStop
";
            controller.Pause();
            controller.WaitForStatus(ServiceControllerStatus.Paused);
            controller.Continue();
            controller.WaitForStatus(ServiceControllerStatus.Running);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
            Assert.Equal(expected, _testService.GetServiceOutput());
        }

        [ConditionalFact(nameof(RunningWithElevatedPrivileges))]
        public void TestOnExecuteCustomCommand()
        {
            var controller = new ServiceController(_testService.TestServiceName);
            AssertExpectedProperties(controller);
            string expected =
@"OnStart args=
OnCustomCommand command=128
OnStop
";
            controller.ExecuteCommand(128);
            controller.WaitForStatus(ServiceControllerStatus.Running);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
            Assert.Equal(expected, _testService.GetServiceOutput());
        }

        [ConditionalFact(nameof(RunningWithElevatedPrivileges))]
        public void TestOnContinueBeforePause()
        {
            var controller = new ServiceController(_testService.TestServiceName);
            AssertExpectedProperties(controller);
            string expected =
@"OnStart args=
OnStop
";
            controller.Continue();
            controller.WaitForStatus(ServiceControllerStatus.Running);
            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped);
            Assert.Equal(expected, _testService.GetServiceOutput());
        }

        public void Dispose()
        {
            _testService.DeleteTestServices();
        }
    }
}
