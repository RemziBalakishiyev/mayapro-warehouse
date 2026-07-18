namespace MayaPro.WarehouseApi.Modules.Exports.Application;

/// <summary>Binary export payload ready for <c>Results.File</c>.</summary>
public sealed record ExportFileResult(byte[] Content, string ContentType, string FileName);
