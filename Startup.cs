using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using CoreAdmin.Data;
using CoreAdmin.Models;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using System.Linq;
using System.Threading.Tasks;

namespace CoreAdmin
{
    public class Startup
    {
        bool UseRequestLocalizationProvider = false;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        void LoadLanguages()
        {
            var En = new LanguageItem() { Id = "", Name = "English", Code = "en", IsRTL = false, FlagIcon = "en-flag.png", CultureCode = "en-US" };
            var Fr = new LanguageItem() { Id = "", Name = "French", Code = "fr", IsRTL = false, FlagIcon = "fr-flag.png", CultureCode = "fr-FR" };
            var Es = new LanguageItem() { Id = "", Name = "Spain", Code = "es", IsRTL = false, FlagIcon = "es-flag.png", CultureCode = "es-ES" };
            var Ar = new LanguageItem() { Id = "", Name = "Arabic", Code = "ar", IsRTL = true, FlagIcon = "ar-flag.png", CultureCode = "ar-SA" };
            Languages.Add(En);
            Languages.Add(Fr);
            Languages.Add(Es);
            Languages.Add(Ar);
        }

        void ConfigureRequestLocalizationProvider(IServiceCollection services)
        {
            LanguageItem[] LangItems = Languages.Items;

            CustomRequestCultureProvider Provider = new CustomRequestCultureProvider(async (HttpContext) => {
                await Task.Yield();
                return new ProviderCultureResult(Session.Language.CultureCode);
            });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new RequestCulture(LangItems[0].CultureCode);
                options.SupportedCultures = LangItems.Select(item => item.GetCulture()).ToList();
                options.SupportedUICultures = options.SupportedCultures;

                //options.RequestCultureProviders.Clear();
                options.RequestCultureProviders.Insert(0, Provider);
            });

        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            LoadLanguages();

            if (UseRequestLocalizationProvider)
            {
                ConfigureRequestLocalizationProvider(services);
            }

            services.AddSession(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddHttpContextAccessor();


            services.Configure<NeptuneCoreSettings>(Configuration.GetSection(NeptuneCoreSettings.SectionName));

            // Note: This line is for demonstration purposes only, I would not recommend using this as a shorthand approach for accessing settings
            // While having to type '.Value' everywhere is driving me nuts (>_<), using this method means reloaded appSettings.json from disk will not work
            services.AddSingleton(s => s.GetRequiredService<IOptions<NeptuneCoreSettings>>().Value);

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
            services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddRoleManager<RoleManager<IdentityRole>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            //Database developer page exception filter
            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddTransient<IEmailSender, EmailSender>();

            services
                .AddControllersWithViews();

            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Identity/Account/Login";
                options.LogoutPath = "/Identity/Account/Logout";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Session.HttpContextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            app.UseSession();


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            if (UseRequestLocalizationProvider)
            {
                // adds the Microsoft.AspNetCore.Localization.RequestLocalizationMiddleware to the pipeline
                app.UseRequestLocalization();
            }
            else
            {
                //app.UseMiddleware<RequestLocalizationCustomMiddleware>();

                app.Use(async (context, next) =>
                {
                    CultureInfo.CurrentCulture = Session.Language.GetCulture();
                    CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;

                    await next.Invoke();
                });
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    "default",
                    "{controller=Authentication}/{action=Login2}");
                endpoints.MapRazorPages();
                endpoints.MapBlazorHub();
            });
        }
    }
}
