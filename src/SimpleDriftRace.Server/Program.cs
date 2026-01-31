using Microsoft.OpenApi.Models;
using Server.Services;
var builder = WebApplication.CreateBuilder(args);
var magiconion = builder.Services.AddMagicOnion();
if (builder.Environment.IsDevelopment())
{
    magiconion.AddJsonTranscoding();
    builder.Services.AddMagicOnionJsonTranscodingSwagger();
}
builder.Services.AddSwaggerGen(options => {
    options.IncludeMagicOnionXmlComments(Path.Combine(AppContext.BaseDirectory, "SimpleDriftRace.Shared.xml"));
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "タイトル",
        Description = "説明",
    });
});
builder.Services.AddMvcCore().AddApiExplorer();
builder.Services.AddSingleton<RoomContextRepository>();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "タイトル");
    });
}
app.MapMagicOnionService();
app.MapGet("/", () => "");

app.Run();
