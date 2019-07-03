// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.IdentityServer4.AzureStorage.Helpers
{
    public static class ExceptionHelper
    {
        public static void LogStorageExceptions(AggregateException aggregate, 
            Action<Microsoft.Azure.Cosmos.Table.StorageException> tableStorageLogger= null,
            Action<Microsoft.Azure.Storage.StorageException> blobStorageLogger = null)
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
                    Microsoft.Azure.Storage.StorageException blobException = ex as Microsoft.Azure.Storage.StorageException;
                    if (blobException != null)
                    {
                        blobStorageLogger?.Invoke(blobException);
                    }
                }
            }
        }
    }
}
