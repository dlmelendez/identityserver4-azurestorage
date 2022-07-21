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
        //TODO: Holy shit fix this before checking in.
        public static void LogStorageExceptions(AggregateException aggregate, 
            Action<RequestFailedException> tableStorageLogger= null,
            Action<RequestFailedException> blobStorageLogger = null)
        {
            if (aggregate.InnerExceptions != null)
            {
                foreach (Exception ex in aggregate.InnerExceptions)
                {
                    RequestFailedException requestFailedException = ex as RequestFailedException;
                    if (ex != null)
                    {
                        if (tableStorageLogger != null)
                        {
                            //Azure.Data.Tables.Models.TableErrorCode.
                            //tableStorageLogger?.Invoke(tableStorageException);
                        }
                        if (blobStorageLogger != null)
                        {
                            //blobStorageLogger?.Invoke(blobException);
                        }
                    }
                }
            }
        }
    }
}
