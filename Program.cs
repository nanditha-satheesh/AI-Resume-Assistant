using AIResumeAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure anti-forgery to accept tokens via header (for AJAX/JSON requests)
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Configure upload size limit (5 MB to match controller validation)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024;
});

// Register application services
builder.Services.AddSingleton<IPdfParserService, PdfParserService>();
builder.Services.AddSingleton<IResumeSessionService, ResumeSessionService>();
builder.Services.AddHttpClient<IOpenAIService, GroqService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
