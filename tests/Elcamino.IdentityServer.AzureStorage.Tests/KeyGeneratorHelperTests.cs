// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using ElCamino.IdentityServer.AzureStorage.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ElCamino.IdentityServer.AzureStorage.Tests
{
    [TestClass]
    public class KeyGeneratorHelperTests
    {
        [TestMethod]
        public void GenerateDateTimeDecendingId_Equal()
        {
            DateTime now = DateTime.UtcNow;
            Console.WriteLine(now.ToLongDateString() + " " + now.ToLongTimeString());
            string nowBlobName = KeyGeneratorHelper.GenerateDateTimeDecendingId(now);
            Console.WriteLine($"now blob: {nowBlobName}");

            DateTime old = now.AddMinutes(-1);
            Console.WriteLine(old.ToLongDateString() + " " + old.ToLongTimeString());
            string oldBlobName = KeyGeneratorHelper.GenerateDateTimeDecendingId(old);
            Console.WriteLine($"old blob: {oldBlobName}");

            DateTime newer = now.AddMinutes(5);
            Console.WriteLine(newer.ToLongDateString() + " " + newer.ToLongTimeString());
            string newBlobName = KeyGeneratorHelper.GenerateDateTimeDecendingId(newer);
            Console.WriteLine($"newer blob: {newBlobName}");


            int nowToOld = String.Compare(nowBlobName, oldBlobName);
            Console.WriteLine($"String.Compare(nowBlobName, oldBlobName): {nowToOld}");
            Assert.AreEqual<int>(-1, nowToOld);

            int oldToNow = String.Compare(oldBlobName, nowBlobName);
            Console.WriteLine($"String.Compare(oldBlobName, nowBlobName): {oldToNow}");
            Assert.AreEqual<int>(1, oldToNow);

            int nowToNewer = String.Compare(nowBlobName, newBlobName);
            Console.WriteLine($"String.Compare(nowBlobName, newBlobName): {nowToNewer}");
            Assert.AreEqual<int>(1, nowToNewer);

            int newerToNow = String.Compare(newBlobName, nowBlobName);
            Console.WriteLine($"String.Compare(newBlobName, nowBlobName): {newerToNow}");
            Assert.AreEqual<int>(-1, newerToNow);

            Assert.AreEqual<int>(0, String.Compare(nowBlobName, nowBlobName));
        }

        [TestMethod]
        [DataRow("test")]
        [DataRow("Test2")]
        [DataRow(KeyGeneratorHelper.MaxSha1Hash)]
        [DataRow(KeyGeneratorHelper.MinSha1Hash)]
        public void GenerateKeyHashes_Equal(string testValue)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var hashedKey = KeyGeneratorHelper.GenerateHashValue(testValue);
            stopwatch.Stop();
            Console.WriteLine($"{nameof(KeyGeneratorHelper)}.{nameof(KeyGeneratorHelper.GenerateHashValue)}({testValue}): {stopwatch.Elapsed.TotalMilliseconds} ms");

            stopwatch.Restart();
            var hashedKey2 = KeyGeneratorHelper.GenerateHashValue_Deprecated(testValue);
            stopwatch.Stop();
            Console.WriteLine($"{nameof(KeyGeneratorHelper)}.{nameof(KeyGeneratorHelper.GenerateHashValue_Deprecated)}({testValue}): {stopwatch.Elapsed.TotalMilliseconds} ms");

            Assert.AreEqual<string>(hashedKey2, hashedKey.ToString());
        }
    }
}
