// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Azure;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.Duende.IdentityServer.AzureStorage.Helpers
{
    public static class ExceptionHelper
    {
        public static void LogStorageExceptions(AggregateException aggregate, 
            Action<Microsoft.Azure.Cosmos.Table.StorageException> tableStorageLogger= null,
            Action<RequestFailedException> blobStorageLogger = null)
        {
            if (aggregate.InnerExceptions != null)
            {
                foreach (Exception ex in aggregate.InnerExceptions)
                {
                    Microsoft.Azure.Cosmos.Table.StorageException tableStorageException = ex as Microsoft.Azure.Cosmos.Table.StorageException;
                    if (tableStorageException != null)
                    {
                        tableStorageLogger?.Invoke(tableStorageException);
                    }
                    RequestFailedException blobException = ex as RequestFailedException;
                    if (blobException != null)
                    {
                        blobStorageLogger?.Invoke(blobException);
                    }
                }
            }
        }
    }
}
