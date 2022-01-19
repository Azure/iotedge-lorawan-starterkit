// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;

    internal class FileStreamWithContentLengthResult : FileStreamResult, IActionResult
    {
        private readonly long contentLength;

        public FileStreamWithContentLengthResult(Stream fileStream, string contentType, long contentLength) : base(fileStream, contentType)
        {
            this.contentLength = contentLength;
        }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var executor = context.HttpContext.RequestServices.GetRequiredService<IActionResultExecutor<FileStreamResult>>();
            context.HttpContext.Response.ContentLength = this.contentLength;
            return executor.ExecuteAsync(context, this);
        }
    }
}
