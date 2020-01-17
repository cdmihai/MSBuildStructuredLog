﻿using System;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Tests;
using Xunit;
using Xunit.Abstractions;
using static StructuredLogger.Tests.TestUtilities;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests : IDisposable
    {
        private static string s_testProject = @"
         <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
            <PropertyGroup>
               <TestProperty>Test</TestProperty>
            </PropertyGroup>

            <ItemGroup>
               <TestItem Include=""Test"" />
            </ItemGroup>

            <Target Name='Target1'>
               <Message Text='MessageOutputText'/>
               <Message Text='[$(MSBuildThisFileFullPath)]'/>
            </Target>

            <Target Name='Target2' AfterTargets='Target1'>
               <Exec Command='echo a'/>
            </Target>

            <Target Name='Target3' AfterTargets='Target2'>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=a' Targets='InnerTarget1'/>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=b' Targets='InnerTarget1'/>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=a' Targets='InnerTarget2'/>
            </Target>

            <Target Name='InnerTarget1'>
               <Message Text='inner target 1'/>
            </Target>

            <Target Name='InnerTarget2'>
               <Message Text='inner target 2'/>
            </Target>
         </Project>";

        public BinaryLoggerTests(ITestOutputHelper output)
        {
        }

        [Fact]
        public void TestBinaryLoggerRoundtrip()
        {
            var binLog = GetFullPath("1.binlog");
            var binaryLogger = new BinaryLogger();
            binaryLogger.Parameters = binLog;
            var buildSuccessful = MSBuild.BuildProject(s_testProject, binaryLogger);

            Assert.True(buildSuccessful);

            var build = Serialization.Read(binLog);
            var xml1 = GetFullPath("1.xml");
            var xml2 = GetFullPath("2.xml");

            Serialization.Write(build, xml1);

            Serialization.Write(build, GetFullPath("1.buildlog"));
            build = Serialization.Read(GetFullPath("1.buildlog"));

            Serialization.Write(build, xml2);

            Assert.False(Differ.AreDifferent(xml1, xml2));

            build = XlinqLogReader.ReadFromXml(xml1);
            Serialization.Write(build, GetFullPath("3.xml"));
            Assert.False(Differ.AreDifferent(xml1, GetFullPath("3.xml")));

            build = Serialization.Read(xml1);
            Serialization.Write(build, GetFullPath("4.xml"));

            Assert.False(Differ.AreDifferent(xml1, GetFullPath("4.xml")));
        }

        private static string GetProperty(Logging.StructuredLogger.Build build)
        {
            var property = build.FindFirstDescendant<Project>().FindChild<Folder>("Properties").FindChild<Property>(p => p.Name == "FrameworkSDKRoot").Value;
            return property;
        }

        public void Dispose()
        {
        }
    }
}