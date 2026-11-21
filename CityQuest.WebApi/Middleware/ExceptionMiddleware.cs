using System.ComponentModel.DataAnnotations;
using CityQuest.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityQuest.WebApi.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request {TraceId} cancelled", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            var (status, title) = exception switch
            {
                NotFoundException => (404, exception.Message),
                ValidationException => (400, exception.Message),
                ConflictException => (409, exception.Message),
                RoutePlanningException => (422, exception.Message),
                RoutingUnavailableException => (503, exception.Message),
                DbUpdateConcurrencyException => (409, "Дані вже змінено. Оновіть сторінку."),
                _ => (500, "Внутрішня помилка сервера.")
            };
            if (status >= 500)
                logger.LogError(exception, "Request {Method} {Path} failed. TraceId: {TraceId}",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);
            else
                logger.LogWarning("Request {Method} {Path} returned {Status}. TraceId: {TraceId}",
                    context.Request.Method, context.Request.Path, status, context.TraceIdentifier);
            if (context.Response.HasStarted)
                throw;
            context.Response.Clear();
            context.Response.StatusCode = status;
            var problem = new ProblemDetails
            {
                Status = status, Title = title, Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            };
            if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                { HttpContext = context, ProblemDetails = problem }))
                await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
