using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.DataUpload
{
    internal record Co2ApiResponse(bool Success, bool Timeout, string? ErrorMessage);
}
