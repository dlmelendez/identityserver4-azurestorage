// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ElCamino.IdentityServer.AzureStorage.Helpers
{
    public static class KeyGeneratorHelper
    {

        public static readonly ETag ETagWildCard = ETag.All;
        public const string MinSha1Hash = "0000000000000000000000000000000000000000";
        public const string MaxSha1Hash = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";
        /// <summary>
        /// As is before hash is calculated
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string ConvertToCaseSensitveHash(string plainText)
        {
            plainText ??= string.Empty;
            return ConvertKeyToHash<SHA1>(SHA1.Create(), plainText);
        }

        /// <summary>
        /// Used for 
        /// </summary>
        /// <param name="plainBytes">Assumes UTF-8 bytes</param>
        /// <returns></returns>
        public static string ConvertToHash(byte[] plainBytes)
        {
            if (plainBytes != null)
            {
                return ConvertBytesToHash<SHA1>(SHA1.Create(), plainBytes);
            }

            return null;
        }

        public static string GenerateDateTimeDecendingId(DateTime dt)
        {
            //Maybe validate UTC somewhere else?
            //if (dt.Kind != DateTimeKind.Utc)
            //    throw new ArgumentException("DateTime must be UTC kind.", nameof(dt));
            TimeSpan ts = DateTime.MaxValue.Subtract(dt);
            return ts.TotalMilliseconds.ToString("000000000000000");
        }

        public static string GenerateHashValue(string plainText)
        {
            return ConvertToCaseSensitveHash(plainText);
        }


        private static string ConvertKeyToHash<T>(T hash, string input) where T : HashAlgorithm
        {
            using (hash)
            {
                return GetHash<T>(hash, input.Trim().ToLower());
            }
        }

        private static string ConvertBytesToHash<T>(T hash, byte[] input) where T : HashAlgorithm
        {
            using (hash)
            {
                return GetHash<T>(hash, input);
            }
        }

        private static string GetHash<T>(T hash, string input) where T : HashAlgorithm
        {
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder(64);

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }

        /// <summary>
        /// Receives UTF-8 byte[] to hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hash">HashAlgorithm usually will be SHA-1 </param>
        /// <param name="input">Assumes UTF-8 encoded data</param>
        /// <returns></returns>
        private static string GetHash<T>(T hash, byte[] input) where T : HashAlgorithm
        {
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = hash.ComputeHash(input);

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder(40);

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }

       
    }
}
