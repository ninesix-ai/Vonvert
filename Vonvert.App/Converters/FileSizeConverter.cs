// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Globalization;
using System.Windows.Data;

namespace Vonvert.App.Converters;

/// <summary>
/// Converts a raw byte count into a kilobyte value so a history row can render
/// "{duration}s • {size} KB" via a StringFormat multi-binding.
/// </summary>
public sealed class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double bytes = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        return bytes / 1024.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
