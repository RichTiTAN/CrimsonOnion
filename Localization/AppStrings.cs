using Avalonia.Controls;

namespace CrimsonOnion.Localization
{
    public static class AppStrings
    {
        public static bool IsPersian { get; private set; } = false;

        public static void SetLanguage(string lang)
        {
            IsPersian = lang == "PERSIAN";
        }


        public static void Apply(TextBlock? tb, string en, string fa, bool forceLtr = false)
        {
            if (tb == null) return;
            tb.Text = IsPersian ? fa : en;
            if (IsPersian)
            {
                tb.FontFamily = new global::Avalonia.Media.FontFamily("Segoe UI");
                tb.FlowDirection = forceLtr
                    ? global::Avalonia.Media.FlowDirection.LeftToRight
                    : global::Avalonia.Media.FlowDirection.RightToLeft;
            }
            else
            {
                tb.FontFamily = global::Avalonia.Media.FontFamily.Default;
                tb.FlowDirection = global::Avalonia.Media.FlowDirection.LeftToRight;
            }
            
            if (forceLtr && IsPersian)
            {
                tb.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
            }
        }

        public static void ApplyBtn(Button? btn, string en, string fa)
        {
            if (btn == null) return;
            btn.Content = IsPersian ? fa : en;
            if (IsPersian)
            {
                btn.FontFamily = new global::Avalonia.Media.FontFamily("Segoe UI");
            }
            else
            {
                btn.FontFamily = global::Avalonia.Media.FontFamily.Default;
            }
        }

        public static void ApplyToolTip(Control? c, string en, string fa)
        {
            if (c == null) return;
            ToolTip.SetTip(c, IsPersian ? fa : en);
        }

        public static string SidebarConnection  => IsPersian ? "اتصال"             : "CONNECTION";
        public static string SidebarCountries   => IsPersian ? "کشورها"            : "COUNTRIES";
        public static string SidebarSplitTunnel => IsPersian ? "اسپلیت تانل"       : "SPLIT TUNNEL";
        public static string SidebarSettings    => IsPersian ? "تنظیمات"           : "SETTINGS";
        public static string SidebarAbout       => IsPersian ? "درباره"            : "ABOUT";

        public static string Connect            => IsPersian ? "اتصال"             : "CONNECT";
        public static string ConnectedBtn       => IsPersian ? "متصل"              : "CONNECTED";
        public static string Disconnect         => IsPersian ? "قطع اتصال"         : "DISCONNECT";
        public static string Disconnected       => IsPersian ? "منتظر اتصال"       : "DISCONNECTED";
        public static string ConnectedFor       => IsPersian ? "متصل برای"         : "CONNECTED FOR";
        public static string ConnectedTo        => IsPersian ? "به"                 : "TO";
        public static string ProxyMode          => IsPersian ? "حالت پروکسی"       : "PROXY MODE";
        public static string VpnMode            => IsPersian ? "حالت VPN"          : "VPN MODE";
        public static string ClearProxy         => IsPersian ? "پروکسی خالی"       : "CLEAR PROXY";
        public static string BridgeType         => IsPersian ? "نوع بریج"          : "BRIDGE TYPE";
        public static string TorEngines         => IsPersian ? "موتورهای Tor"      : "TOR ENGINES";
        public static string LogsStatus         => IsPersian ? "لاگ‌ها و وضعیت"   : "LOGS & STATUS";
        public static string TorBootstrap       => IsPersian ? "راه‌اندازی Tor"    : "TOR BOOTSTRAP";
        public static string XrayLogHeader      => IsPersian ? "اتصالات (لاگ Xray)" : "CONNECTIONS (XRAY LOG)";
        public static string OpenLocalPort      => IsPersian ? "پورت لوکال:"       : "OPEN LOCAL PORT:";
        public static string OpenLanPort        => IsPersian ? "پورت لن:"         : "OPEN LAN PORT:";
        public static string PingLabel          => IsPersian ? "پینگ"              : "Ping";
        public static string TotalLabel         => IsPersian ? "مجموع"             : "Total";
        public static string DownloadLabel      => IsPersian ? "دانلود"            : "Download";
        public static string UploadLabel        => IsPersian ? "آپلود"             : "Upload";
        public static string GetBridges         => IsPersian ? "دریافت بریج"       : "GET BRIDGES";
        public static string EnterCaptcha       => IsPersian ? "کپچا را وارد کنید" : "ENTER CAPTCHA";
        public static string Save               => IsPersian ? "ذخیره"             : "SAVE";
        public static string Cancel             => IsPersian ? "لغو"               : "CANCEL";
        public static string Submit             => IsPersian ? "ارسال"             : "SUBMIT";

        public static string SectionStartup     => IsPersian ? "اجرا"              : "START-UP";
        public static string LaunchOnStartup    => IsPersian ? "اجرا با ویندوز"    : "LAUNCH ON START-UP";
        public static string AutoConnect        => IsPersian ? "اتصال خودکار"      : "AUTO-CONNECT";
        public static string StartMinimized     => IsPersian ? "شروع کوچک‌شده"     : "START MINIMIZED";
        public static string MinimizeToTray     => IsPersian ? "کوچک کردن به tray" : "MINIMIZE TO TRAY";
        public static string SectionConnection  => IsPersian ? "اتصال"             : "CONNECTION";
        public static string CustomXrayExit     => IsPersian ? "نود خروجی سفارشی Xray" : "CUSTOM XRAY EXIT-NODE";
        public static string OutboundProxy      => IsPersian ? "پروکسی خروجی"     : "OUTBOUND PROXY";
        public static string DnsSettings        => IsPersian ? "تنظیمات DNS"       : "DNS SETTINGS";
        public static string AdBlocker          => IsPersian ? "مسدودکننده تبلیغات و ردیاب" : "AD AND TRACKER BLOCKER";
        public static string AllowLan           => IsPersian ? "اجازه اتصالات LAN" : "ALLOW LAN CONNECTIONS";
        public static string SectionSystem      => IsPersian ? "سیستم"             : "SYSTEM";
        public static string LanguageSetting    => IsPersian ? "زبان"              : "LANGUAGE";
        public static string DebugMode          => IsPersian ? "حالت دیباگ"        : "DEBUG MODE";
        public static string DesktopShortcut    => IsPersian ? "میانبر دسکتاپ"     : "DESKTOP SHORTCUT";
        public static string StartMenuShortcut  => IsPersian ? "میانبر منوی استارت" : "START MENU SHORTCUT";
        public static string Create             => IsPersian ? "ایجاد"             : "CREATE";
        public static string UpstreamDohUrl     => IsPersian ? "آدرس DoH بالادست"  : "UPSTREAM DOH URL";
        public static string ProxyType          => IsPersian ? "نوع"               : "TYPE";
        public static string AddressIp          => IsPersian ? "آدرس/IP"           : "ADDRESS/IP";
        public static string Port               => IsPersian ? "پورت"              : "PORT";
        public static string Authentication     => IsPersian ? "احراز هویت"        : "AUTHENTICATION";
        public static string Username           => IsPersian ? "نام کاربری"         : "USERNAME";
        public static string Password           => IsPersian ? "رمز عبور"           : "PASSWORD";
        public static string ImportJson         => IsPersian ? "وارد کردن .JSON"    : "IMPORT .JSON";

        public static string SplitTunneling     => IsPersian ? "اسپلیت تانل"       : "SPLIT TUNNELING";
        public static string Disabled           => IsPersian ? "غیرفعال"           : "DISABLED";
        public static string Exclusive          => IsPersian ? "اختصاصی"           : "EXCLUSIVE";
        public static string Inclusive          => IsPersian ? "شامل"              : "INCLUSIVE";
        public static string DomainsAndIps      => IsPersian ? "دامنه‌ها و IPها"   : "DOMAINS AND IPs";
        public static string Applications       => IsPersian ? "برنامه‌ها"          : "APPLICATIONS";
        public static string BlockedDomains     => IsPersian ? "دامنه‌ها و IPهای مسدود شده" : "BLOCKED DOMAINS AND IPs";
        public static string Add                => IsPersian ? "افزودن"             : "ADD";
        public static string Edit               => IsPersian ? "ویرایش"             : "EDIT";
        public static string Browse             => IsPersian ? "مرور"               : "BROWSE";

        public static string AboutVersion       => IsPersian ? "نسخه"              : "VERSION";
        public static string CheckForUpdates    => IsPersian ? "بررسی برای آپدیت"  : "CHECK FOR UPDATES";
        public static string Donations          => IsPersian ? "کمک‌های مالی"      : "DONATIONS";
        public static string DonationsDesc      => IsPersian ? "اگر می‌خواهید از من یا پروژه حمایت کنید، می‌توانید با ارسال مبلغ دلخواه به یکی از آدرس‌های کیف پول زیر این کار را انجام دهید،" : "if u want to support me or the project you can do so by sending your desired amount to one of these wallet addresses,";

        public static string ExpertTitle        => IsPersian ? "پیکربندی پیشرفته مسیریابی" : "EXPERT ROUTING CONFIGURATION";
        public static string HardwareAccel      => IsPersian ? "شتاب‌دهی سخت‌افزاری:" : "Hardware Accel:";
        public static string FascistFirewall    => IsPersian ? "فایروال سخت‌گیر:"     : "Fascist Firewall:";
        public static string StrictNodes        => IsPersian ? "نودهای سخت‌گیر:"     : "Strict Nodes:";
        public static string CustomTorrcLabel   => IsPersian ? "خطوط سفارشی Torrc (پیشرفته - یک در هر خط):" : "Custom Torrc Lines (Advanced - One per line):";

        public static string TrayNotConnected   => IsPersian ? "متصل نیست"         : "NOT CONNECTED";
        public static string TrayConnected      => IsPersian ? "متصل"              : "CONNECTED";
        public static string TrayClose          => IsPersian ? "بستن برنامه"       : "CLOSE THE APP";
        public static string TrayConnect        => IsPersian ? "اتصال"             : "CONNECT";
        public static string TrayDisconnect     => IsPersian ? "قطع اتصال"         : "DISCONNECT";
        public static string TrayShowWindow     => IsPersian ? "نمایش پنجره"       : "SHOW WINDOW";

        public static string TtCustomXray   => IsPersian ? "از سرور شخصی Xray خود به عنوان نود خروجی بعد از Tor استفاده کنید. مسیر: شما -> Tor -> سرور Xray شما -> اینترنت. سایت‌ها IP سرور Xray شما را می‌بینند نه Tor. یک خروجی JSON جایگذاری کنید یا یک لینک اشتراک‌گذاری (VLESS, VMess, Trojan, SS) وارد کنید. فقط پورت‌های 80 و 443 از طریق Tor کار می‌کنند؛ REALITY, KCP و QUIC مسدود هستند." : "Use a personal Xray server as your exit node after Tor. Path: you → Tor → your Xray server → internet. Websites see your Xray server's IP, not Tor's. Paste outbound JSON or import a share link (VLESS, VMess, Trojan, SS). Only ports 80 and 443 work over Tor; REALITY, KCP, and QUIC are blocked.";
        public static string TtOutboundProxy => IsPersian ? "اتصال خود Tor به شبکه را از طریق یک پروکسی خارجی SOCKS5 یا HTTPS ارسال کنید. از این گزینه زمانی استفاده کنید که Tor مسدود است و برای رسیدن به نودهای نگهبان به پروکسی نیاز دارید. این روی نحوه اتصال Tor تأثیر می‌گذارد، نه اینکه مرور شما از کدام کشور خارج می‌شود." : "Send Tor's own connection to the network through an external SOCKS5 or HTTPS proxy. Use this when Tor is blocked and you need a proxy just to reach guard nodes. This affects how Tor boots up—not which country your browsing exits from.";
        public static string TtDnsSettings  => IsPersian ? "حل‌وفصل DNS از طریق DNS-over-HTTPS (DoH) رمزگذاری‌شده به جای کوئری‌های متنی. کاهش نشت DNS و سانسور. یک ارائه‌دهنده انتخاب کنید یا یک URL سفارشی DoH وارد کنید؛ در حالت پروکسی (Xray) و حالت VPN (sing-box) اعمال می‌شود." : "Resolve DNS through encrypted DNS-over-HTTPS (DoH) instead of plaintext queries. Reduces DNS leaks and censorship. Pick a provider or enter a custom DoH URL; applies in proxy mode (Xray) and VPN mode (sing-box).";
        public static string TtAdBlocker    => IsPersian ? "مسدود کردن درخواست‌ها به دامنه‌های شناخته‌شده تبلیغات و ردیاب‌ها قبل از خروج از رایانه شما. Xray دامنه‌های مطابق را به یک خروجی نامعتبر (blackhole) هدایت می‌کند. فقط بر ترافیک عبوری از پروکسی محلی تأثیر می‌گذارد، نه برنامه‌هایی که از اسپلیت تانل عبور نمی‌کنند." : "Drop requests to known ad and tracker domains before they leave your PC. Xray routes matching domains to a blackhole outbound. Only affects traffic going through the local proxy—not apps on split-tunnel bypass.";
        public static string TtAllowLan     => IsPersian ? "به دستگاه‌های دیگر در شبکه خود اجازه دهید از این رایانه به عنوان پروکسی استفاده کنند. وقتی روشن است، پروکسی محلی روی تمام رابط‌ها (0.0.0.0) گوش می‌دهد؛ وقتی خاموش است، فقط همین دستگاه (127.0.0.1) می‌تواند متصل شود. فقط در شبکه‌هایی که به آنها اعتماد دارید روشن کنید." : "Let other devices on your network use this PC as a proxy. When on, the local proxy listens on all interfaces (0.0.0.0); when off, only this machine (127.0.0.1) can connect. Turn on only on networks you trust.";
        public static string TtLanguage     => IsPersian ? "تغییر زبان برنامه. برای اعمال کامل تغییرات ممکن است نیاز به باز کردن مجدد برنامه باشد." : "Change the application language. Reopening the app may be required for all changes to take effect.";
        public static string TtDebugMode    => IsPersian ? "پنجره‌های کنسول مربوط به Tor، Xray، HAProxy و sing-box را به جای پنهان کردن نمایش می‌دهد. فقط برای عیب‌یابی کاربرد دارد." : "Show console windows for Tor, Xray, HAProxy, and sing-box instead of hiding them. Helpful for reading live logs when something fails to connect. Does not change routing or security-only visibility.";
        
        public static string TtProxyMode => IsPersian ? "ترافیک سیستم را از طریق یک پروکسی محلی هدایت میکند. ایده آل برای عبور از فیلترینگ بدون تغییر مسیر کل سیستم." : "Routes system traffic through a local proxy. Ideal for bypassing censorship without changing global system routing.";
        public static string TtVpnMode => IsPersian ? "تمام ترافیک سیستم را به یک کارت شبکه مجازی هدایت میکند تا به اجبار همه برنامه ها از پروکسی عبور کنند." : "Routes all system traffic through a virtual network interface (TUN), forcing all applications to use the proxy.";
        public static string TtClearProxy => IsPersian ? "پروکسی سیستم را غیرفعال میکند اما پورت محلی را باز نگه میدارد، بنابراین میتوانید برنامه ها را به صورت دستی تنظیم کنید تا از پروکسی استفاده کنند." : "Disables the system proxy but keeps the local port open, so you can manually configure specific applications to use the proxy.";
        
        public static string TtSplitDis     => IsPersian ? "اسپلیت تانل غیرفعال است." : "Split tunneling is disabled.";
        public static string TtSplitExc     => IsPersian ? "فقط برنامه ها دامنه ها و ای پی های لیست شده در اینجا از پروکسی مستثنی می شوند." : "Only bypass the proxy for the apps, domains and IPs listed here.";
        public static string TtSplitInc     => IsPersian ? "فقط برنامه ها دامنه ها و ای پی های لیست شده در اینجا از طریق پروکسی هدایت می شوند." : "Only route the apps, domains and IPs listed here through the proxy.";

        public static string SplitExplanationExclusive => IsPersian ? "فقط برنامه ها، دامنه ها و آیپی های لیست شده در اینجا از پروکسی مستثنی می شوند." : "Only bypass the proxy for the apps, domains, and IPs listed below.";
        public static string SplitExplanationInclusive => IsPersian ? "فقط برنامه ها، دامنه ها و آیپی های لیست شده در اینجا از طریق پروکسی هدایت می شوند." : "Only route the apps, domains, and IPs listed below through the proxy.";
    }
}

