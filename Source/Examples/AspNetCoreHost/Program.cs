﻿using Microsoft.AspNetCore.Builder;
using Quartzmin;

const string virtialPathRoot = "/quartzmin";

var builder = WebApplication.CreateBuilder();

builder.Services.AddQuartzmin(virtialPathRoot, "test");

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Items["header"] = "a";
    await next.Invoke(context).ConfigureAwait(false);
});

app.UseQuartzmin(new QuartzminOptions
{
    Scheduler = DemoScheduler.CreateAsync().Result
});

app.UseRouting();
app.MapGet("/", () => "Hello");

app.Run();