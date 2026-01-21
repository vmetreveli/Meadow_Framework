namespace Meadow.Framework.Exceptions.Infrastructure.Exceptions;

/// <summary>
/// </summary>
/// <param name="logger"></param>
public class RequestResponseLoggingMiddleware(ILoggerFactory logger) : IMiddleware
{
    private static readonly string[] NotToLogKeys =
        { "password", "currentPassword", "repeatedPassword", "newPassword", "confirmNewPassword" };

    private static readonly string[] NotToLogHeaders = { };
    private readonly ILogger _logger = logger.CreateLogger(nameof(RequestResponseLoggingMiddleware));

    /// <summary>
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string? messageId = context.Items["MessageId"]!.ToString();
        StreamReader requestStream = new(context.Request.Body);
        string requestBodyString;

        if (context.Request.HasFormContentType)
        {
            requestBodyString = HandleFormDataRequest(context);
        }
        else
        {
            requestBodyString = HandleRegularJsonRequest(await requestStream.ReadToEndAsync());
            context.Request.Body.Seek(0, SeekOrigin.Begin);
        }

        Dictionary<string, StringValues> headers = context.Request.Headers.ToDictionary(x => x.Key, y => y.Value);

        foreach (string notToLogHeader in NotToLogHeaders) headers.Remove(notToLogHeader);

        _logger.LogInformation("{MessageId} {Method} {QueryString} {Headers} {RequestBody}",
            messageId,
            context.Request.Method,
            context.Request.QueryString.Value,
            headers,
            requestBodyString);

        await using MemoryStream ms = new();
        Stream originalResponseBodyStream = context.Response.Body;
        context.Response.Body = ms;

        try
        {
            await next(context);

            ms.Seek(0, SeekOrigin.Begin);
            using StreamReader streamReader = new(ms);
            string responseBodyString = await streamReader.ReadToEndAsync();
            ms.Seek(0, SeekOrigin.Begin);

            await ms.CopyToAsync(originalResponseBodyStream);

            _logger.LogInformation("{MessageId} {ResponseBody}", messageId, responseBodyString);
        }
        catch
        {
            context.Response.Body = originalResponseBodyStream;
            throw;
        }
    }


    private string HandleFormDataRequest(HttpContext context)
    {
        JObject jObject = new();

        ICollection<string> formKeys = context.Request.Form.Keys;

        foreach (string key in formKeys)
            if (context.Request.Form.TryGetValue(key, out StringValues s))
                jObject.Add(key, s.ToString());

        return jObject.ToString();
    }


    private string HandleRegularJsonRequest(string originalBodyString)
    {
        JObject jObject = new();
        string requestBodyString = default;

        try
        {
            jObject = JObject.Parse(originalBodyString);

            foreach (string key in NotToLogKeys)
                if (jObject.ContainsKey(key))
                    jObject.Remove(key);

            requestBodyString = jObject.ToString();
        }
        catch
        {
            // ignored
        }

        return requestBodyString;
    }
}