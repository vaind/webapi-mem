using Microsoft.Diagnostics.NETCore.Client;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

EventPipeProvider[] Providers =
    [
        // 89142578365 == https://github.com/getsentry/perfview/blob/dbf41bda75e163c4aac5590a78c5dade9a744ba3/src/TraceEvent/Parsers/ClrTraceEventParser.cs#L201-L203
        // new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, 89142578365),
        new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Informational),
        new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational),
    ];

var client = new DiagnosticsClient(Environment.ProcessId);
using var session = client.StartEventPipeSession(Providers, requestRundown: false, circularBufferMB: 16);
using var eventSource = TraceLog.CreateFromEventPipeSession(session);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
// Process() blocks until the session is stopped so we need to run it on a separate thread.
Task.Factory.StartNew(eventSource.Process, TaskCreationOptions.LongRunning)
    .ContinueWith(_ =>
    {
        if (_.Exception != null)
        {
            Console.WriteLine(_.Exception);
        }
    }, TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

long eventsReceived = 0;
eventSource.AllEvents += (TraceEvent obj) =>
{
    eventsReceived++;
    if (eventsReceived % 1000 == 0)
    {
        Console.WriteLine($"Events received: {eventsReceived / 1000}k");
    }
};

app.Run();
