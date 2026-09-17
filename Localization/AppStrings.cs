/*
 * CrimsonOnion - A GUI client that runs multiple Tor instances and load-balances them.
 * Copyright (C) 2026 RichTiTAN
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

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

        public static void Apply(TextBlock? tb, string text, bool forceLtr = false, bool keepFont = false, bool leftAlign = false)
        {
            if (tb == null) return;
            tb.Text = text;
            if (IsPersian)
            {
                if (!keepFont) tb.FontFamily = new global::Avalonia.Media.FontFamily("Segoe UI");
                tb.FlowDirection = forceLtr
                    ? global::Avalonia.Media.FlowDirection.LeftToRight
                    : global::Avalonia.Media.FlowDirection.RightToLeft;
            }
            else
            {
                if (!keepFont) tb.FontFamily = global::Avalonia.Media.FontFamily.Default;
                tb.FlowDirection = global::Avalonia.Media.FlowDirection.LeftToRight;
            }

            if (IsPersian && (forceLtr || leftAlign))
            {
                tb.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
            }
        }

        public static void ApplyBtn(Button? btn, string text)
        {
            if (btn == null) return;
            btn.Content = text;
            if (IsPersian)
            {
                btn.FontFamily = new global::Avalonia.Media.FontFamily("Segoe UI");
                btn.FlowDirection = global::Avalonia.Media.FlowDirection.RightToLeft;
            }
            else
            {
                btn.FontFamily = global::Avalonia.Media.FontFamily.Default;
                btn.FlowDirection = global::Avalonia.Media.FlowDirection.LeftToRight;
            }
        }

        public static void ApplyToolTip(Control? c, string text)
        {
            if (c == null) return;
            if (IsPersian)
            {
                var tb = new global::Avalonia.Controls.TextBlock
                {
                    Text            = text,
                    FlowDirection   = global::Avalonia.Media.FlowDirection.RightToLeft,
                    FontFamily      = new global::Avalonia.Media.FontFamily("Segoe UI"),
                    TextWrapping    = global::Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth        = 300
                };
                ToolTip.SetTip(c, tb);
            }
            else
            {
                ToolTip.SetTip(c, text);
            }
        }
        // ==================================================
        // SIDEBAR
        // ==================================================
        public static string SidebarCountries   => IsPersian ? "کشورها"            : "COUNTRIES";
        public static string SidebarSplitTunnel => IsPersian ? "اسپلیت تانل"       : "SPLIT TUNNEL";
        public static string SidebarSettings    => IsPersian ? "تنظیمات"           : "SETTINGS";
        public static string SidebarAbout       => IsPersian ? "درباره"            : "ABOUT";
        public static string SidebarThemes      => IsPersian ? "تم ها" : "THEMES";

        // ==================================================
        // MAIN UI & STATUS
        // ==================================================
        public static string Connect            => IsPersian ? "اتصال"             : "CONNECT";
        public static string ConnectedBtn       => IsPersian ? "متصل"              : "CONNECTED";
        public static string Disconnect         => IsPersian ? "قطع اتصال"         : "DISCONNECT";
        public static string Disconnected       => IsPersian ? "منتظر اتصال"       : "Disconnected";
        public static string SessionLabel       => IsPersian ? "نشست"             : "SESSION:";
        public static string LocationLabel      => IsPersian ? "کشور"             : "LOCATION:";
        public static string ProxyMode          => IsPersian ? "حالت پروکسی"       : "PROXY MODE";
        public static string VpnMode            => IsPersian ? "حالت VPN"          : "VPN MODE";
        public static string ClearProxy         => IsPersian ? "بدون پروکسی"       : "CLEAR PROXY";
        public static string BridgeType         => IsPersian ? "نوع بریج"          : "BRIDGE TYPE";
        public static string TorEngines         => IsPersian ? "تعداد تور"      : "TOR ENGINES";
        public static string LogsStatus         => IsPersian ? "لاگ‌ها و وضعیت"   : "LOGS & STATUS";
        public static string TorBootstrap       => IsPersian ? "راه اندازی تور"    : "TOR BOOTSTRAP";
        public static string XrayLogHeader      => IsPersian ? "اتصالات (لاگ Xray)" : "CONNECTIONS (XRAY LOG)";
        public static string OpenLocalPort      => IsPersian ? "پورت لوکال"       : "LOCAL PORT:";
        public static string OpenLanPort        => IsPersian ? "پورت لن"         : "LAN PORT:";
        public static string PingLabel          => IsPersian ? "پینگ"              : "PING";
        public static string TotalLabel         => IsPersian ? "مجموع"             : "TOTAL";
        public static string DownloadLabel      => IsPersian ? "دانلود"            : "DOWNLOAD";
        public static string UploadLabel        => IsPersian ? "آپلود"             : "UPLOAD";
        public static string GetBridges         => IsPersian ? "دریافت بریج"       : "GET BRIDGES";
        public static string Save               => IsPersian ? "ذخیره"             : "SAVE";
        public static string Submit             => IsPersian ? "ثبت"               : "SUBMIT";
        public static string CaptchaVerifying   => IsPersian ? "در حال بررسی..."   : "VERIFYING...";
        public static string TorStatusOffline   => IsPersian ? "آفلاین"             : "OFFLINE";
        public static string TorStatusBooting   => IsPersian ? "در حال اجرا..."     : "BOOTING...";
        public static string TorStatusWaiting   => IsPersian ? "منتظر..."           : "WAITING...";
        public static string GeoTracing             => IsPersian ? "در حال جستجو..." : "Tracing...";
        public static string GeoTimeout             => IsPersian ? "ناموفق"          : "Timeout";
        public static string RoutingOptimized   => IsPersian ? "بهینه"             : "OPTIMIZED";
        public static string RoutingExpert      => IsPersian ? "حرفه ای"           : "EXPERT";
		public static string BtnBrowse => IsPersian ? "مرور" : "BROWSE";
		public static string BtnConnecting => IsPersian ? "در حال اتصال..." : "CONNECTING";

        // ==================================================
        // THEMES OVERLAY
        // ==================================================
        public static string ThemesPauseGlow => IsPersian ? "توقف انیمیشن‌ها" : "PAUSE ANIMATIONS";
        public static string ThemesDisableGlow => IsPersian ? "غیرفعال‌سازی انیمیشن‌ها" : "DISABLE ANIMATIONS";

        // ==================================================
        // SETTINGS OVERLAY
        // ==================================================
        public static string SectionStartup     => IsPersian ? "اجرا"              : "START-UP";
        public static string LaunchOnStartup    => IsPersian ? "اجرا با ویندوز"    : "LAUNCH ON START-UP";
        public static string AutoConnect        => IsPersian ? "اتصال خودکار"      : "AUTO-CONNECT";
        public static string StartMinimized     => IsPersian ? "شروع کوچک‌شده"     : "START MINIMIZED";
        public static string MinimizeToTray     => IsPersian ? "کوچک کردن به tray" : "MINIMIZE TO TRAY";
        public static string CustomXrayExit     => IsPersian ? "نود خروجی Xray" : "CUSTOM XRAY EXIT-NODE";
        public static string OutboundProxy      => IsPersian ? "پروکسی خروجی"     : "OUTBOUND PROXY";
        public static string AdapterBinding     => IsPersian ? "اتصال به آداپتور" : "BIND ADAPTER";
        public static string ScanAdapters       => IsPersian ? "اسکن"            : "SCAN";
        public static string LbPolicy           => IsPersian ? "سیاست توزیع بار" : "LOAD-BALANCE POLICY";
        public static string DnsSettings        => IsPersian ? "تنظیمات DNS"       : "DNS SETTINGS";
        public static string AdBlocker          => IsPersian ? "مسدودکننده تبلیغات و ردیاب" : "AD AND TRACKER BLOCKER";
        public static string AllowLan           => IsPersian ? "اجازه اتصالات LAN" : "ALLOW LAN CONNECTIONS";
        public static string LanAuth            => IsPersian ? "احراز هویت"        : "AUTHENTICATION";
        public static string SectionSystem      => IsPersian ? "سیستم"             : "SYSTEM";
        public static string LanguageSetting    => IsPersian ? "زبان"              : "LANGUAGE";
        public static string DebugMode          => IsPersian ? "حالت دیباگ"        : "DEBUG MODE";
        public static string DesktopShortcut    => IsPersian ? "میانبر دسکتاپ"     : "DESKTOP SHORTCUT";
        public static string StartMenuShortcut  => IsPersian ? "میانبر منوی استارت" : "START MENU SHORTCUT";
        public static string Create             => IsPersian ? "ایجاد"             : "CREATE";
        public static string UpstreamDohUrl     => IsPersian ? "آدرس DoH بالادست"  : "UPSTREAM DOH URL";
        public static string SystemDns          => IsPersian ? "DNS سیستم"          : "SYSTEM DNS";
        public static string SystemDnsPrimary   => IsPersian ? "DNS اول"            : "PRIMARY DNS";
        public static string SystemDnsSecondary => IsPersian ? "DNS دوم"            : "SECONDARY DNS";
        public static string ProxyType          => IsPersian ? "نوع"               : "TYPE";
        public static string AddressIp          => IsPersian ? "آدرس/IP"           : "ADDRESS/IP";
        public static string Port               => IsPersian ? "پورت"              : "PORT";
        public static string Authentication     => IsPersian ? "احراز هویت"        : "AUTHENTICATION";
        public static string Username           => IsPersian ? "نام کاربری"         : "USERNAME";
        public static string Password           => IsPersian ? "رمز عبور"           : "PASSWORD";

        // ==================================================
        // SPLIT TUNNEL OVERLAY
        // ==================================================
        public static string SplitTunneling     => IsPersian ? "اسپلیت تانل"       : "SPLIT TUNNELING";
        public static string SplitTunnelDirectUDP => IsPersian ? "UDP مستقیم" : "DIRECT UDP";
        public static string Disabled           => IsPersian ? "غیرفعال"           : "DISABLED";
        public static string Exclusive          => IsPersian ? "اختصاصی"           : "EXCLUSIVE";
        public static string Inclusive          => IsPersian ? "شامل"              : "INCLUSIVE";
        public static string DomainsAndIps      => IsPersian ? "دامنه‌ها، IPها و پورت‌ها" : "DOMAINS, IPs & PORTS";
        public static string Applications       => IsPersian ? "برنامه‌ها"          : "APPLICATIONS";
        public static string BlockedDomains     => IsPersian ? "دامنه‌ها، IPها و پورت‌های مسدود شده" : "BLOCKED DOMAINS, IPs & PORTS";
        public static string Add                => IsPersian ? "افزودن"             : "ADD";
        public static string Edit               => IsPersian ? "ویرایش"             : "EDIT";
		public static string LblSplitAppsWarning => IsPersian ? "هشدار: حساس به حروف بزرگ و کوچک" : "Warning: Case sensitive";

        // ==================================================
        // ABOUT & UPDATES
        // ==================================================
        public static string AboutVersion       => IsPersian ? "نسخه"              : "VERSION";
        public static string CheckForUpdates    => IsPersian ? "بررسی برای آپدیت"  : "CHECK FOR UPDATES";
        public static string UpdateChecking     => IsPersian ? "در حال بررسی برای آپدیت..." : "CHECKING FOR UPDATES...";
        public static string UpdateLatest       => IsPersian ? "آخرین نسخه نصب شده است" : "LATEST VERSION INSTALLED";
        public static string UpdateManual       => IsPersian ? "نیاز به آپدیت دستی" : "MANUAL UPDATE REQUIRED";
        public static string UpdateCancelled    => IsPersian ? "آپدیت لغو شد" : "UPDATE CANCELLED";
        public static string UpdateAutoTitle    => IsPersian ? "آپدیت موجود است" : "UPDATE AVAILABLE";
        public static string UpdateAutoMsg      => IsPersian ? "نسخه جدید (v{0}) آماده نصب است! مایلید الان آپدیت کنید؟" : "A new version of CrimsonOnion (v{0}) is ready to install! Would you like to update now?";
        public static string UpdateManualTitle  => IsPersian ? "نیاز به آپدیت دستی" : "MANUAL UPDATE REQUIRED";
        public static string UpdateManualMsg    => IsPersian ? "نسخه v{0} موجود است! نسخه فعلی شما برای آپدیت خودکار خیلی قدیمی است. لطفا آخرین نسخه را از گیت‌هاب دانلود کنید." : "CrimsonOnion v{0} is available! Your current version is too old to safely auto-update. Please download the latest release from GitHub.";
        public static string BtnUpdateNow       => IsPersian ? "همین الان آپدیت کن" : "UPDATE NOW";
        public static string BtnDownloadGithub  => IsPersian ? "دانلود از گیت‌هاب" : "DOWNLOAD FROM GITHUB";
        public static string BtnChangeLog       => IsPersian ? "تغییرات" : "CHANGE LOG";
        public static string BtnCancel => IsPersian ? "لغو" : "CANCEL";
        public static string AboutCreator => IsPersian ? "سازنده: RichTitan" : "Creator: @RichTitan";
        public static string AboutLicense => IsPersian ? "لایسنس: GPL-3.0 license" : "License: GPL-3.0 license";
        public static string AboutOtherApps => IsPersian ? "برنامه‌های دیگر" : "OTHER APPS";

        // ==================================================
        // TRAY MENU
        // ==================================================
		public static string TrayStatusNotConnected => IsPersian ? "متصل نیست" : "NOT CONNECTED";
		public static string TrayBtnCloseApp => IsPersian ? "بستن برنامه" : "CLOSE THE APP";
		public static string TrayBtnShowWindow => IsPersian ? "نمایش پنجره" : "SHOW WINDOW";

        // ==================================================
        // PROMOTIONS & MISC
        // ==================================================
        public static string DonationsTitle     => IsPersian ? "حمایت مالی"         : "DONATIONS";
        public static string DonationsDesc      => IsPersian ? "به دلیل حفظ حریم خصوصی، کمک‌های مالی تنها از طریق کیف پول‌های رمزارز امکان‌پذیر است." : "Donations are only available through Crypto wallets due to privacy reasons.";
        public static string ExpertTitle        => IsPersian ? "پیکربندی پیشرفته مسیریابی" : "EXPERT ROUTING CONFIGURATION";
        public static string PromoCrimsonXTitle => IsPersian ? "کشف CrimsonX" : "Discover CrimsonX";
        public static string PromoCrimsonXHeader => IsPersian ? "برنامه جدید ما CrimsonX منتشر شد!" : "Our new app CrimsonX was released!";
        public static string PromoCrimsonXMsg => IsPersian ? "کریمسون‌ایکس (CrimsonX) یک کلاینت رابط کاربری VPN است که چندین کانفیگ xray مناسب برای ارائه‌دهنده اینترنت شما را دریافت، تست و بالانس می‌کند." : "CrimsonX is a GUI VPN client that fetches, tests and load-balances multiple xray configs suited for your specific ISP and internet.";
        public static string BtnClose => IsPersian ? "بستن" : "Close";

        // ==================================================
        // TOOLTIPS
        // ==================================================
        public static string TtLbPolicy         => IsPersian
            ? "نحوه توزیع اتصالات بین نمونه‌های Tor توسط Xray را کنترل می‌کند."
            : "Controls how Xray distributes connections across your Tor instances.";
        public static string TtLanAuth          => IsPersian
            ? "اگر فعال باشد، دستگاه‌های روی شبکه باید نام کاربری و رمز عبور وارد کنند تا از این پروکسی استفاده کنند. فقط در حالت پروکسی و Clear Proxy اعمال می‌شود."
            : "When enabled, devices on the network must supply a username and password to use this proxy. Only applies in Proxy and Clear Proxy mode.";
        public static string SplitTunnelDirectUDPTooltip => IsPersian ? "ترافیک UDP را مستقیم و بدون عبور از شبکه تور به اینترنت ارسال می‌کند. این ترافیک تونل نخواهد شد، بنابراین این گزینه ناشناس بودن را کاهش می‌دهد. این گزینه می‌تواند به بازی‌های ویدیویی، چت صوتی دیسکورد یا سایر پلتفرم‌های وابسته به UDP کمک کند." : "Bypass Tor and route all UDP traffic directly to the internet adapter. UDP traffic will not be tunneled, so this option reduces anonymity. This option can help with video games, discord voice or other udp dependant platforms.";
        public static string TtCustomXray   => IsPersian ? "از سرور شخصی Xray خود به عنوان نود خروجی بعد از Tor استفاده کنید. مسیر: شما -> Tor -> سرور Xray شما -> اینترنت. سایت‌ها IP سرور Xray شما را می‌بینند نه Tor. یک خروجی JSON جایگذاری کنید یا یک لینک اشتراک‌گذاری (VLESS, VMess, Trojan, SS) وارد کنید. فقط پورت‌های 80 و 443 از طریق Tor کار می‌کنند؛ REALITY, KCP و QUIC مسدود هستند." : "Use a personal Xray server as your exit node after Tor. Path: you → Tor → your Xray server → internet. Websites see your Xray server's IP, not Tor's. Paste outbound JSON or import a share link (VLESS, VMess, Trojan, SS). Only ports 80 and 443 work over Tor; REALITY, KCP, and QUIC are blocked.";
        public static string TtOutboundProxy => IsPersian ? "کل اتصال Tor را از طریق یک پروکسی خروجی SOCKS5 یا HTTPS خارجی عبور می‌دهد. زمانی که Tor مسدود است و برای رسیدن به گره‌های محافظ به پروکسی نیاز دارید، از این استفاده کنید. این بر نحوه بوت شدن Tor تأثیر می‌گذارد، نه اینکه مرور شما از کدام کشور خارج می‌شود." : "Send Tor's own connection to the network through an external SOCKS5 or HTTPS proxy. Use this when Tor is blocked and you need a proxy just to reach guard nodes. This affects how Tor boots up—not which country your browsing exits from.";
        public static string TtAdapterBinding => IsPersian ? "کل ترافیک Tor را مجبور می‌کند منحصراً از طریق آداپتور شبکه انتخاب شده خارج شود. زمانی که پل \"Snowflake\" انتخاب شده باشد کار نمی‌کند." : "Forces all Tor traffic to exclusively exit through the selected network adapter. Does not work when \"Snowflake\" bridge is selected.";
        public static string TtDnsSettings  => IsPersian ? "تنظیمات DNS رمزگذاری‌شده را کنترل می‌کند. DoH: DNS را از طریق HTTPS رمزگذاری می‌کند تا نشت و سانسور کاهش یابد؛ برای حالت پروکسی (Xray) و VPN (sing-box) اعمال می‌شود. DNS سیستم: DNS آداپتور شبکه اصلی ویندوز را هنگام اتصال تغییر می‌دهد تا Tor بتواند بوت‌استرپ کند؛ پس از قطع اتصال یا بستن برنامه بازگردانده می‌شود." : "Controls encrypted DNS settings. DoH: resolves DNS over HTTPS to reduce leaks and censorship; applies in proxy mode (Xray) and VPN mode (sing-box). System Proxy DNS: changes the Windows DNS on your main adapter at connect time so Tor can bootstrap; restored on disconnect or app close.";
        public static string TtAdBlocker    => IsPersian ? "مسدود کردن درخواست‌ها به دامنه‌های شناخته‌شده تبلیغات و ردیاب‌ها قبل از خروج از رایانه شما. Xray دامنه‌های مطابق را به یک خروجی نامعتبر (blackhole) هدایت می‌کند. فقط بر ترافیک عبوری از پروکسی محلی تأثیر می‌گذارد، نه برنامه‌هایی که از اسپلیت تانل عبور نمی‌کنند." : "Drop requests to known ad and tracker domains before they leave your PC. Xray routes matching domains to a blackhole outbound. Only affects traffic going through the local proxy—not apps on split-tunnel bypass.";
        public static string TtAllowLan     => IsPersian ? "به دستگاه‌های دیگر در شبکه خود اجازه دهید از این رایانه به عنوان پروکسی استفاده کنند. وقتی روشن است، پروکسی محلی روی تمام رابط‌ها (0.0.0.0) گوش می‌دهد؛ وقتی خاموش است، فقط همین دستگاه (127.0.0.1) می‌تواند متصل شود. فقط در شبکه‌هایی که به آنها اعتماد دارید روشن کنید." : "Let other devices on your network use this PC as a proxy. When on, the local proxy listens on all interfaces (0.0.0.0); when off, only this machine (127.0.0.1) can connect. Turn on only on networks you trust.";
        public static string TtLanguage     => IsPersian ? "تغییر زبان برنامه. برای اعمال کامل تغییرات ممکن است نیاز به باز کردن مجدد برنامه باشد." : "Change the application language. Reopening the app may be required for all changes to take effect.";
        public static string TtDebugMode    => IsPersian ? "گزارش‌های زنده Tor، Xray (پشت و جلو)، dnstt-client و sing-box را در لحظه ضبط می‌کند. برای عیب‌یابی در هنگام قطع اتصال مفید است. تأثیری بر مسیریابی یا امنیت ندارد." : "Captures live logs from Tor, Xray (frontend & backend), dnstt-client, and sing-box. Helpful for diagnosing issues when something fails to connect. Does not change routing or security.";
        public static string TtSystemDns    => IsPersian
            ? "DNS ویندوز آداپتور شبکه اصلی را هنگام اتصال تغییر می‌دهد تا Tor بتواند از آن استفاده کند. پس از قطع اتصال یا بستن برنامه، DNS قبلی بازگردانده می‌شود."
            : "Changes the Windows DNS of your main network adapter when you connect, so Tor bootstrap benefits from it. Restored to original on disconnect or app close.";
        public static string TtProxyMode => IsPersian ? "ترافیک سیستم را از طریق یک پروکسی محلی هدایت میکند. ایده آل برای عبور از فیلترینگ بدون تغییر مسیر کل سیستم." : "Routes system traffic through a local proxy. Ideal for bypassing censorship without changing global system routing.";
        public static string TtVpnMode => IsPersian ? "تمام ترافیک سیستم را به یک کارت شبکه مجازی هدایت میکند تا به اجبار همه برنامه ها از پروکسی عبور کنند." : "Routes all system traffic through a virtual network interface (TUN), forcing all applications to use the proxy.";
        public static string TtClearProxy => IsPersian ? "پروکسی سیستم را غیرفعال میکند اما پورت محلی را باز نگه میدارد، بنابراین میتوانید برنامه ها را به صورت دستی تنظیم کنید تا از پروکسی استفاده کنند." : "Disables the system proxy but keeps the local port open, so you can manually configure specific applications to use the proxy.";
        public static string TtSplitDis     => IsPersian ? "اسپلیت تانل غیرفعال است." : "Split tunneling is disabled.";
        public static string TtSplitExc     => IsPersian ? "فقط برنامه ها، دامنه ها، ای پی ها و پورت های لیست شده در اینجا از پروکسی مستثنی می شوند." : "Only bypass the proxy for the apps, domains, IPs and ports listed here.";
        public static string TtSplitInc     => IsPersian ? "فقط ترافیک این برنامه‌ها، دامنه‌ها، IPها و پورت‌ها از پروکسی عبور می‌کند." : "Only route the apps, domains, IPs and ports listed here through the proxy.";
        public static string TtLaunchOnStartup => IsPersian ? "اجرای خودکار برنامه هنگام ورود به ویندوز." : "Automatically launch the application when Windows starts.";
        public static string TtAutoConnect => IsPersian ? "اتصال خودکار به شبکه هنگام اجرای برنامه." : "Automatically connect to the Tor network when the application is launched.";
        public static string TtStartMinimized => IsPersian ? "اجرای برنامه به صورت کوچک شده (مخفی)." : "Start the application minimized in the background.";
        public static string TtMinimizeToTray => IsPersian ? "کوچک کردن برنامه در سینی سیستم به جای نوار وظیفه." : "Minimize the application to the system tray instead of the taskbar.";
        public static string TtPingRefresh => IsPersian ? "برای به‌روزرسانی پینگ کلیک کنید" : "Click to refresh ping";
        public static string TtLocationRefresh => IsPersian ? "برای به‌روزرسانی موقعیت و پینگ کلیک کنید" : "Click to refresh location and ping";
        public static string TtLbLeastLoad => IsPersian ? "هر اتصال جدید را به خلوت‌ترین نمونه Tor ارسال می‌کند. بهترین گزینه برای ترافیک ترکیبی با حجم متغیر." : "Distributes each new connection to the least-loaded Tor instance. Best for mixed traffic with varying connection size.";
        public static string TtLbRoundRobin => IsPersian ? "اتصالات را به طور مساوی و به نوبت بین تمام نمونه‌های Tor توزیع می‌کند. مناسب برای توزیع یکنواخت و برابر." : "Distributes connections evenly across all Tor instances in order, cycling through them one by one. Good for consistent, equal distribution.";
        public static string TtLbLeastPing => IsPersian ? "نمونه Tor با کمترین پینگ اخیر را انتخاب می‌کند. بهترین گزینه برای ترافیک حساس به تأخیر." : "Picks the Tor instance with the lowest recent ping. Best for latency-sensitive traffic.";
        public static string TtLbRandom => IsPersian ? "برای هر اتصال جدید یک نمونه Tor را به صورت تصادفی انتخاب می‌کند. در طول زمان متعادل است اما نوسان بیشتری نسبت به Round Robin دارد." : "Picks a Tor instance at random for each new connection. Statistically even over time but with more variance than Round Robin.";
        public static string TtDisabledVpnSnowflake => IsPersian ? "غیرفعال است چون پل Snowflake انتخاب شده است. اگر می‌خواهید از Snowflake در حالت VPN استفاده کنید، لطفاً گزینه \"Direct UDP\" را در منوی تونل‌زنی دوگانه روشن کنید." : "Disabled because Snowflake bridge is selected.\nIf you want to use Snowflake in VPN Mode please turn on the \"Direct UDP\" option in split tunneling menu.";

        // ==================================================
        // TOASTS
        // ==================================================
        public static string ToastLatestVersion => IsPersian ? "شما از قبل آخرین نسخه را دارید!" : "You are already on the latest version!";
        public static string ToastVpnDisabledSnowflake => IsPersian ? "حالت VPN برای پل Snowflake غیرفعال شد. اگر می‌خواهید از Snowflake در حالت VPN استفاده کنید، لطفاً گزینه \"Direct UDP\" را در منوی اسپلیت تانل روشن کنید." : "VPN Mode disabled for Snowflake bridge. To use it, turn on \"Direct UDP\" in split tunneling.";
        public static string ToastAdapterBindingSnowflake => IsPersian ? "وقتی پل \"Snowflake\" انتخاب شده، اتصال به آداپتور کار نمی‌کند." : "BIND ADAPTER will not work when Snowflake bridge is selected.";
        public static string ToastRealityNotSupported => IsPersian ? "کانفیگ های REALITY بر روی Tor پشتیبانی نمی شوند." : "REALITY configs are not supported over Tor.";
        public static string ToastKcpQuicNotSupported => IsPersian ? "کانکشن های KCP و QUIC بر روی Tor پشتیبانی نمی شوند." : "KCP and QUIC transports are not supported over Tor.";
        public static string ToastPortsSupported => IsPersian ? "تنها پورت های 80 و 443 پشتیبانی می شوند." : "Only port 80 and 443 are supported.";
        public static string ToastXrayRejected => IsPersian ? "کانفیگ Xray رد شد. " : "Xray config rejected. ";
        public static string ToastInvalidJson => IsPersian ? "سینتکس نامعتبر JSON مربوط به Xray!" : "Invalid Xray JSON syntax!";
        public static string ToastLinkConverted => IsPersian ? "لینک به صورت خودکار به JSON تبدیل شد!" : "Link auto-converted to JSON!";
        public static string ToastFailedImport => IsPersian ? "وارد کردن JSON ناموفق بود." : "Failed to import JSON.";
        public static string ToastTaskFailed => IsPersian ? "عملیات ناموفق بود: " : "Task failed: ";
        public static string ToastShortcutCreated => IsPersian ? "شورتکات با موفقیت ایجاد شد!" : "Shortcut created successfully!";
        public static string ToastShortcutFailed => IsPersian ? "ایجاد شورتکات ناموفق بود." : "Failed to create shortcut.";
        public static string ToastReconnectChanges  => IsPersian ? "لطفا برای اعمال تغییرات مجددا متصل شوید." : "Please reconnect to apply the changes.";
        public static string ToastReconnectDns      => IsPersian ? "برای اعمال تغییرات DNS دوباره متصل شوید." : "Reconnect to apply the DNS changes.";
        public static string ToastAddressCopied     => IsPersian ? "آدرس در کلیپ بورد کپی شد!" : "Address copied to clipboard!";
        public static string ToastUpdateDownloadFailed => IsPersian ? "ارتباط هنگام دانلود بروزرسانی قطع شد." : "Connection lost while downloading update.";
		public static string ToastUpdateCheckFailed => IsPersian ? "ارتباط هنگام بررسی بروزرسانی قطع شد." : "Connection lost while checking for updates.";
		public static string ToastUpdateErrorFormat => IsPersian ? "خطا در بروزرسانی: {0}" : "Update Error: {0}";
		public static string ToastAdapterNotAvailable => IsPersian ? "آداپتور انتخاب شده در دسترس نیست!" : "Selected adapter is not available!";
		public static string ToastDirectUdpAdapterNotAvailable => IsPersian ? "آداپتور Direct UDP انتخاب شده در دسترس نیست!" : "Selected Direct UDP adapter is not available!";
		public static string ToastDirectUdpAdapterLost => IsPersian ? "آداپتور شبکه قبلی که برای Direct UDP انتخاب کرده بودید دیگر در دسترس نیست." : "Your previously selected network adapter for Direct UDP is no longer available.";
		public static string ToastCopiedToClipboard => IsPersian ? "کپی شد!" : "Copied to clipboard!";
		public static string ToastFailedToReachTor => IsPersian ? "اتصال به سرورهای تور با مشکل مواجه شد. لطفا از ربات تلگرامی @GetBridgesBot یا ایمیل bridges@torproject.org برای دریافت پل استفاده کنید." : "Failed to reach Tor servers. Please use the @GetBridgesBot Telegram bot or email bridges@torproject.org to get a bridge.";
		public static string ToastSelectedAdapterLost => IsPersian ? "آداپتور شبکه انتخابی شما دیگر در دسترس نیست." : "Your previously selected network adapter is no longer available.";
		public static string ToastVpnAdapterInUse => IsPersian ? "آداپتور VPN از قبل توسط یک برنامه دیگر در حال استفاده است!" : "VPN adapter is already in use by another app!";
		public static string ToastFirstConnectionLong => IsPersian ? "اتصال اولیه ممکن است بیشتر طول بکشد، لطفا صبر کنید." : "First connection might take longer, please wait.";
		public static string ToastEngineStartFailedFormat => IsPersian ? "خطا در استارت موتور: {0}" : "Engine start failed: {0}";
		public static string ToastBootstrappingLongFormat => IsPersian ? "اتصال پس از {0} دقیقه هنوز برقرار نشده. در حال تلاش..." : "Still bootstrapping after {0} minutes. Continuing to try...";
		public static string ToastDnsttBridgeWarning1 => IsPersian ? "برای اعمال پل مجددا متصل شوید.\nهشدار: پل DNSTT تنظیمات پروکسی و آداپتور را دور میزند." : "Reconnect to apply bridge.\nWarning: DNSTT bypasses proxy and adapter settings.";
		public static string ToastDnsttBridgeWarning2 => IsPersian ? "هشدار: پل های DNSTT تنظیمات پروکسی و آداپتور را دور میزنند." : "Warning: DNSTT bridges bypass Outbound Proxy and Adapter Binding settings.";
		public static string ToastInvalidPrimaryDns => IsPersian ? "لطفا یک آدرس IPv4 معتبر برای DNS اولیه وارد کنید." : "Please enter a valid IPv4 address for the primary DNS.";
		public static string ToastInvalidSecondaryDns => IsPersian ? "لطفا یک آدرس IPv4 معتبر برای DNS ثانویه وارد کنید." : "Please enter a valid IPv4 address for the secondary DNS.";
		public static string ToastEnterUsername => IsPersian ? "لطفا نام کاربری را وارد کنید." : "Please enter a username.";
		public static string ToastCredentialsSaved => IsPersian ? "اطلاعات ورود ذخیره شد." : "Credentials saved.";
		public static string ToastDnsttTunnelFailed => IsPersian ? "راه‌اندازی تونل DNSTT ناموفق بود." : "Failed to start the DNSTT tunnel.";
}
}
