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
            Action<RequestFailedException> storageLogger)
        {
            if (aggregate.InnerExceptions != null)
            {
                foreach (Exception ex in aggregate.InnerExceptions)
                {
                    RequestFailedException storageException = ex as RequestFailedException;
                    if (ex != null)
                    {
                        if (storageLogger != null)
                        {
                            storageLogger?.Invoke(storageException);
                        }
                    }
                }
            }
        }
    }
}
