// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages
{
    public class ErrorMessage
    {
        public string Message { get; set; }
        public string Path { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
