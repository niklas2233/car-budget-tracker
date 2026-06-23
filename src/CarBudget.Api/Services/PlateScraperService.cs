using CarBudget.Api.DTOs;
using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CarBudget.Api.Services;

public class PlateScraperService
{
    private const string RateLimitedSentinel = "__RATE_LIMITED__";

    private static readonly SemaphoreSlim BrowserInitLock = new(1, 1);
    private static readonly object LogLock = new();
    private static readonly ConcurrentDictionary<string, Task<string?>> InflightLookupFetches = new(StringComparer.OrdinalIgnoreCase);
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    internal static string LogFilePath = "";
    internal static bool DebugSaveHtml = false;

    private static void Log(string message)
    {
        if (string.IsNullOrEmpty(LogFilePath)) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        try { lock (LogLock) { File.AppendAllText(LogFilePath, line); } }
        catch { }
    }

    public static string NormalizePlate(string plate) =>
        Regex.Replace(plate.ToUpperInvariant(), "[^A-Z0-9]", "");

    // Returns the parsed DTO, RateLimitedSentinel string, or null on failure.
    // Callers check the string return from FetchHtmlAsync to distinguish rate-limit from error.
    public async Task<(VehicleLookupDto? Result, bool RateLimited, bool Failed)> LookupAsync(
        string normalizedPlate, string url, bool forceRefresh)
    {
        var html = await FetchRenderedPageHtmlDeduplicatedAsync(url, normalizedPlate, forceRefresh);

        if (html == RateLimitedSentinel)
        {
            Log($"LOOKUP FAILED  plate={normalizedPlate}  reason=Rate limited by car.info");
            return (null, RateLimited: true, Failed: false);
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            Log($"LOOKUP FAILED  plate={normalizedPlate}  reason=Playwright returned null/empty HTML");
            return (null, RateLimited: false, Failed: true);
        }

        Log($"LOOKUP HTML   plate={normalizedPlate}  htmlBytes={html.Length}");

        var dto = await ParseCarInfoLookup(html, normalizedPlate, url);

        Log($"LOOKUP PARSE  plate={normalizedPlate}  make={dto.Make}  model={dto.Model}  year={dto.Year}  color={dto.ColorName}  gearbox={dto.Gearbox}  fuel={dto.FuelType}  mileageKm={dto.MileageKm}  specsCount={dto.Specifications.Count}");

        return (dto, RateLimited: false, Failed: false);
    }

    private static Task<string?> FetchRenderedPageHtmlDeduplicatedAsync(string url, string normalizedPlate, bool forceRefresh)
    {
        if (forceRefresh)
            return TryFetchRenderedPageHtmlAsync(url, normalizedPlate);

        return InflightLookupFetches.GetOrAdd(normalizedPlate, _ => FetchAndReleaseAsync(normalizedPlate, url));
    }

    private static async Task<string?> FetchAndReleaseAsync(string normalizedPlate, string url)
    {
        try
        {
            return await TryFetchRenderedPageHtmlAsync(url, normalizedPlate);
        }
        finally
        {
            InflightLookupFetches.TryRemove(normalizedPlate, out _);
        }
    }

    private static async Task<IBrowser> GetOrCreateBrowserAsync()
    {
        if (_browser is { IsConnected: true })
            return _browser;

        await BrowserInitLock.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true })
                return _browser;

            _playwright ??= await Playwright.CreateAsync();

            var browserPath = GetBundledOrSystemBrowserPath();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            };

            if (!string.IsNullOrEmpty(browserPath))
                launchOptions.ExecutablePath = browserPath;

            _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
            return _browser;
        }
        finally
        {
            BrowserInitLock.Release();
        }
    }

    // Prefer the Playwright-bundled Chromium (set via PLAYWRIGHT_BROWSERS_PATH by the
    // Electron host or Dockerfile). Fall back to a system Edge/Chrome only when no
    // bundled browser is found so the app works without any browser pre-installed.
    private static string? GetBundledOrSystemBrowserPath()
    {
        // Playwright resolves its own Chromium from PLAYWRIGHT_BROWSERS_PATH automatically
        // when ExecutablePath is null — so we only need to return null and let it handle it
        // if the env var is set.
        var playwrightBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrEmpty(playwrightBrowsersPath))
            return null; // Playwright will find it on its own

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var windowsCandidates = new[]
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(localAppData, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(localAppData, @"Google\Chrome\Application\chrome.exe"),
        };

        foreach (var candidate in windowsCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var linuxCandidates = new[]
        {
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/usr/bin/google-chrome",
            "/usr/bin/google-chrome-stable",
        };

        foreach (var candidate in linuxCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null; // Let Playwright use its own bundled Chromium
    }

    private static async Task<string?> TryFetchRenderedPageHtmlAsync(string url, string normalizedPlate = "")
    {
        try
        {
            Log($"PLAYWRIGHT    navigating to {url}");
            var browser = await GetOrCreateBrowserAsync();

            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "en-US",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1600, Height = 2400 }
            });

            var page = await context.NewPageAsync();

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            Log($"PLAYWRIGHT    DOM loaded, waiting for #specifications selector");

            bool specsFound = true;
            try
            {
                await page.WaitForSelectorAsync("#specifications .sprow, #specifications span.sptitle", new PageWaitForSelectorOptions
                {
                    Timeout = 6000
                });
            }
            catch (TimeoutException)
            {
                specsFound = false;
            }

            if (!specsFound)
            {
                var timeoutHtml = await page.ContentAsync();
                DumpFailedHtml(url, timeoutHtml);
                if (IsRateLimited(timeoutHtml))
                {
                    Log("PLAYWRIGHT    RATE LIMITED - coffee break page detected");
                    return RateLimitedSentinel;
                }
                Log("PLAYWRIGHT    TIMEOUT: #specifications selector not found after 6s");
                return null;
            }

            Log($"PLAYWRIGHT    #specifications selector found");

            foreach (var expandLabel in new[] { "Visa fler", "Vis fler", "Vis mer", "Show more" })
            {
                var expandButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = expandLabel });
                if (await expandButton.CountAsync() > 0 && await expandButton.First.IsVisibleAsync())
                {
                    Log($"PLAYWRIGHT    clicking expand button \"{expandLabel}\"");
                    await expandButton.First.ClickAsync();
                    await page.WaitForTimeoutAsync(1200);
                    break;
                }
            }

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 3000 });
            var html = await page.ContentAsync();
            Log($"PLAYWRIGHT    content captured  bytes={html?.Length ?? 0}");
            if (DebugSaveHtml && !string.IsNullOrEmpty(html))
                DumpDebugHtml(normalizedPlate, url, html);
            return html;
        }
        catch (TimeoutException tex)
        {
            Log($"PLAYWRIGHT    TIMEOUT: {tex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Log($"PLAYWRIGHT    ERROR: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static bool IsRateLimited(string? html)
    {
        if (string.IsNullOrEmpty(html)) return false;
        return html.Contains("coffee", StringComparison.OrdinalIgnoreCase)
            || html.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
            || html.Contains("429", StringComparison.Ordinal);
    }

    private static void DumpDebugHtml(string normalizedPlate, string url, string html)
    {
        if (string.IsNullOrEmpty(LogFilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath)!;
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var platePart = string.IsNullOrEmpty(normalizedPlate) ? "unknown" : normalizedPlate;
            var file = Path.Combine(dir, $"lookup_debug_{platePart}_{ts}.html");
            lock (LogLock) { File.WriteAllText(file, $"<!-- URL: {url} -->\n{html}"); }
            Log($"PLAYWRIGHT    debug HTML saved to {file}");
        }
        catch { }
    }

    private static void DumpFailedHtml(string url, string? html)
    {
        if (string.IsNullOrEmpty(LogFilePath) || string.IsNullOrEmpty(html)) return;
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath)!;
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var file = Path.Combine(dir, $"lookup_failed_{ts}.html");
            lock (LogLock) { File.WriteAllText(file, $"<!-- URL: {url} -->\n{html}"); }
            Log($"PLAYWRIGHT    failed HTML saved to {file}");
        }
        catch { }
    }

    private static async Task<VehicleLookupDto> ParseCarInfoLookup(string html, string normalizedPlate, string sourceUrl)
    {
        var result = new VehicleLookupDto
        {
            LicensePlate = normalizedPlate,
            SourceUrl = sourceUrl
        };

        foreach (Match match in Regex.Matches(html, "<script[^>]*type=\"application/ld\\+json\"[^>]*>([\\s\\S]*?)</script>", RegexOptions.IgnoreCase))
        {
            var json = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("mainEntity", out var mainEntity))
                    continue;

                if (mainEntity.TryGetProperty("brand", out var brand) && brand.TryGetProperty("name", out var brandName))
                    result.Make = brandName.GetString();

                if (result.Make == null && mainEntity.TryGetProperty("manufacturer", out var manufacturer))
                    result.Make = manufacturer.GetString();

                if (mainEntity.TryGetProperty("vehicleModelDate", out var yearNode) && int.TryParse(yearNode.GetString(), out var year))
                    result.Year = year;

                if (mainEntity.TryGetProperty("color", out var colorNode))
                    result.ColorName = colorNode.GetString();

                if (mainEntity.TryGetProperty("description", out var descriptionNode))
                    result.Specifications["description"] = descriptionNode.GetString() ?? string.Empty;

                if (mainEntity.TryGetProperty("vehicleEngine", out var engineNode) && engineNode.TryGetProperty("fuelType", out var fuelNode))
                    result.FuelType = fuelNode.GetString();

                if (mainEntity.TryGetProperty("mileageFromOdometer", out var mileageNode) && mileageNode.TryGetProperty("value", out var mileageValue))
                {
                    var mileageText = mileageValue.GetString();
                    var mileageMatch = mileageText == null ? null : Regex.Match(mileageText, @"([0-9][0-9\s,\.]*)");
                    if (mileageMatch != null && mileageMatch.Success)
                    {
                        var digits = mileageMatch.Groups[1].Value.Replace(" ", "").Replace(",", "").Replace(".", "").TrimEnd();
                        if (int.TryParse(digits, out var rawValue))
                        {
                            string? unitCode = null, unitText = null;
                            if (mileageNode.TryGetProperty("unitCode", out var unitCodeNode))
                                unitCode = unitCodeNode.GetString();
                            if (mileageNode.TryGetProperty("unitText", out var unitTextNode))
                                unitText = unitTextNode.GetString();

                            var isMil =
                                string.Equals(unitCode, "SMI", StringComparison.OrdinalIgnoreCase)
                                || (unitText != null && unitText.Trim().Equals("mil", StringComparison.OrdinalIgnoreCase))
                                || (mileageText != null && Regex.IsMatch(mileageText, @"\bmil\b", RegexOptions.IgnoreCase));

                            result.MileageKm = isMil ? rawValue * 10 : rawValue;
                            Log($"MILEAGE JSONLD  raw={rawValue}  unitCode={unitCode ?? "(none)"}  unitText={unitText ?? "(none)"}  valueText={mileageText}  isMil={isMil}  result={result.MileageKm}");
                        }
                    }
                }

                if (root.TryGetProperty("breadcrumb", out var breadcrumb) &&
                    breadcrumb.TryGetProperty("itemListElement", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (!item.TryGetProperty("position", out var positionNode))
                            continue;

                        int position = -1;
                        if (positionNode.ValueKind == JsonValueKind.Number)
                            positionNode.TryGetInt32(out position);
                        else if (positionNode.ValueKind == JsonValueKind.String)
                            int.TryParse(positionNode.GetString(), out position);

                        if (position != 3)
                            continue;

                        if (item.TryGetProperty("item", out var nestedItem) && nestedItem.TryGetProperty("name", out var modelNode))
                        {
                            result.Model = modelNode.GetString();
                            break;
                        }
                    }
                }

                if (result.Model == null && mainEntity.TryGetProperty("name", out var nameNode))
                {
                    var fullName = nameNode.GetString();
                    if (!string.IsNullOrWhiteSpace(fullName) && !string.IsNullOrWhiteSpace(result.Make))
                    {
                        var trimmed = fullName.StartsWith(result.Make + " ", StringComparison.OrdinalIgnoreCase)
                            ? fullName[(result.Make.Length + 1)..]
                            : fullName;

                        result.Model = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    }
                }

                break;
            }
            catch (JsonException) { }
        }

        result.Specifications = ExtractSpecs(html);
        var specs = result.Specifications;

        if (result.Make == null)
        {
            var makeRaw = GetSpecAny(specs, "Merke", "Fabrikat", "Märke");
            if (!string.IsNullOrWhiteSpace(makeRaw))
                result.Make = makeRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        if (result.Model == null)
        {
            var modelRaw = GetSpecAny(specs, "Modell");
            if (!string.IsNullOrWhiteSpace(modelRaw))
                result.Model = modelRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        if (result.Year == null)
        {
            var yearRaw = GetSpecAny(specs, "Årsmodell", "Modellår", "Årgång", "Modellår");
            if (!string.IsNullOrWhiteSpace(yearRaw))
            {
                var yearMatch = Regex.Match(yearRaw, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var parsedYear))
                    result.Year = parsedYear;
            }
        }

        if (result.Make == null || result.Model == null || result.Year == null)
        {
            var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                var titleText = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                Log($"PARSE TITLE   \"{titleText}\"");

                var segments = Regex.Split(titleText, @"\s*[|–]\s*|\s+-\s+");
                var carSegment = segments
                    .Select(s => s.Trim())
                    .FirstOrDefault(s =>
                        s.Length > 8 &&
                        !Regex.IsMatch(s, @"^[A-Z0-9]{4,10}$", RegexOptions.IgnoreCase) &&
                        !s.Contains("car.info", StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;

                Log($"PARSE TITLE   car segment: \"{carSegment}\"");

                if (!string.IsNullOrWhiteSpace(carSegment))
                {
                    var words = carSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2)
                    {
                        result.Make ??= words[0];
                        result.Model ??= words[1];
                    }
                    if (result.Year == null)
                    {
                        var yearInTitle = Regex.Match(carSegment, @"\b(19|20)\d{2}\b");
                        if (yearInTitle.Success && int.TryParse(yearInTitle.Value, out var ty))
                            result.Year = ty;
                    }
                }
            }
        }

        result.InTraffic ??= GetSpecAny(specs, "I trafik", "I trafikk", "In traffic");
        result.SwedishSold ??= GetSpecAny(specs, "Svensksåld", "Norsksolgt", "Norsk-solgt", "Sold in Sweden", "Sold in Norway");
        result.ColorName ??= GetSpecAny(specs, "Färg", "Farge", "Color", "Colour");
        result.OwnerCount ??= GetSpecAny(specs, "Antal ägare", "Antall eiere", "Owners", "Number of owners");
        result.Vin ??= GetSpecAny(specs, "Chassinummer (vin)", "Chassisnummer (vin)", "VIN", "Chassis number (VIN)", "Chassis number");
        result.BodyType ??= GetSpecAny(specs, "Kaross", "Karosseri", "Body type", "Body");
        result.Classification ??= GetSpecAny(specs, "Klassificering", "Klassifisering", "Classification");
        result.Generation ??= GetSpecAny(specs, "Generation");
        result.Engine ??= GetSpecAny(specs, "Motor", "Engine");
        result.Gearbox ??= GetSpecAny(specs, "Växellåda", "Girkasse", "Girboks", "Transmission", "Gearbox");
        result.DriveTrain ??= GetSpecAny(specs, "Drivlina", "Drivlinje", "Drivsystem", "Drivning", "Drivhjul", "Drive train", "Drivetrain");
        result.FuelConsumptionMixed ??= GetSpecAny(specs, "Blandad förbrukning", "Blandet forbruk", "Kombinert forbruk", "Blandad", "Mixed consumption", "Combined consumption");
        result.Co2Mixed ??= GetSpecAny(specs, "CO₂, Blandad", "CO₂, blandet", "CO2, blandet", "CO₂, Mixed", "CO2, Mixed");
        result.CargoVolume ??= GetSpecAny(specs, "Bagagevolym", "Bagasjevolum", "Cargo volume", "Trunk volume", "Boot volume");
        result.SeatCount ??= GetSpecAny(specs, "Antal sittplatser", "Antall sitteplasser", "Seats", "Seat count", "Number of seats");
        result.FuelType ??= GetSpecAny(specs, "Bränsle", "Drivstoff", "Fuel", "Fuel type") ?? result.FuelType;

        var plainText = CleanHtmlText(html);

        result.ColorName ??= ExtractFact(plainText, "Färg", "Antal ägare")
            ?? ExtractFact(plainText, "Farge", "Antall eiere")
            ?? ExtractFact(plainText, "Color", "Owners")
            ?? ExtractFact(plainText, "Colour", "Owners");

        if (result.MileageKm == null)
        {
            var mileage = GetSpecAny(specs, "Mätarställning", "Kilometerstand", "Mileage", "Odometer")
                ?? ExtractFact(plainText, "Mätarställning", "Rapporterade mätarställningar")
                ?? ExtractFact(plainText, "Kilometerstand", "Rapporterte")
                ?? ExtractFact(plainText, "Mileage", "Reported mileages")
                ?? ExtractFact(plainText, "Odometer", "Reported");
            if (!string.IsNullOrWhiteSpace(mileage))
            {
                Log($"MILEAGE SPEC    raw=\"{mileage}\"");
                var mileageKm = ParseMileageFromText(mileage);
                Log($"MILEAGE SPEC    parsed={mileageKm?.ToString() ?? "(null)"}");
                if (mileageKm.HasValue)
                    result.MileageKm = mileageKm;
            }
        }

        result.Gearbox ??= ExtractFactByNextLabels(plainText, "Växellåda", "Drivlina", "Drivning", "Drivhjul", "Drive train", "Body type", "Acceleration", "Utrustningsnivå", "Blandad förbrukning", "CO₂, Blandad", "Fetched", "Updated", "Source", "All captured specs");
        result.Gearbox ??= ExtractFactByNextLabels(plainText, "Girkasse", "Drivlinje", "Drivsystem", "Drive train", "Body type", "Acceleration", "Utstyrsnivå", "Blandet forbruk", "CO₂, blandet", "Fetched", "Updated", "Source", "All captured specs");
        result.DriveTrain ??= ExtractFactByNextLabels(plainText, "Drivlina", "Drive train", "Body type", "Acceleration", "Utrustningsnivå", "Blandad förbrukning", "CO₂, Blandad", "Tankvolym", "Bagagevolym", "Fetched", "Updated", "Source", "All captured specs");
        result.DriveTrain ??= ExtractFactByNextLabels(plainText, "Drivning", "Drive train", "Body type", "Acceleration", "Utrustningsnivå", "Blandad förbrukning", "CO₂, Blandad", "Tankvolym", "Bagagevolym", "Fetched", "Updated", "Source", "All captured specs");
        result.DriveTrain ??= ExtractFactByNextLabels(plainText, "Drivhjul", "Drive train", "Body type", "Acceleration", "Utrustningsnivå", "Blandad förbrukning", "CO₂, Blandad", "Tankvolym", "Bagagevolym", "Fetched", "Updated", "Source", "All captured specs");
        result.DriveTrain ??= ExtractFactByNextLabels(plainText, "Drivlinje", "Drive train", "Body type", "Acceleration", "Utstyrsnivå", "Blandet forbruk", "CO₂, blandet", "Tankvolym", "Bagasjevolum", "Fetched", "Updated", "Source", "All captured specs");
        result.DriveTrain ??= ExtractFactByNextLabels(plainText, "Drivsystem", "Drive train", "Body type", "Acceleration", "Utstyrsnivå", "Blandet forbruk", "CO₂, blandet", "Tankvolym", "Bagasjevolum", "Fetched", "Updated", "Source", "All captured specs");

        return result;
    }

    private static Dictionary<string, string> ExtractSpecs(string html)
    {
        var specs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static bool IsNoisySpecValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (value.Length > 300) return true;
            var contaminationMarkers = new[] { "Läs mer och beställ", "Visa alla", "Dölj alla", "Information markerat i blått", "All captured specs", "Topplista", "Utrustningslista", "Fordonsskatt", "Besökstrend" };
            return contaminationMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        static void TryAddSpec(Dictionary<string, string> target, string labelRaw, string valueRaw)
        {
            var label = CleanHtmlText(labelRaw);
            var value = CleanHtmlText(valueRaw);
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value)) return;
            if (label.Length > 120) return;
            if (IsNoisySpecValue(value)) return;
            if (!target.ContainsKey(label)) target[label] = value;
        }

        foreach (Match match in Regex.Matches(html, "<div[^>]*>\\s*<div[^>]*>(?<label>[^<]+)</div>\\s*<div[^>]*>(?<value>[\\s\\S]*?)</div>\\s*</div>", RegexOptions.IgnoreCase))
            TryAddSpec(specs, match.Groups["label"].Value, match.Groups["value"].Value);

        foreach (Match match in Regex.Matches(html, "<div[^>]*class=[\"'][^\"']*sprow[^\"']*[\"'][^>]*>\\s*<span[^>]*class=[\"'][^\"']*sptitle[^\"']*[\"'][^>]*>(?<label>[\\s\\S]*?)</span>\\s*<span[^>]*class=[\"'][^\"']*ast-i[^\"']*[\"'][^>]*>(?<value>[\\s\\S]*?)</span>", RegexOptions.IgnoreCase))
            TryAddSpec(specs, match.Groups["label"].Value, match.Groups["value"].Value);

        foreach (Match match in Regex.Matches(html, "<div[^>]*class=[\"'][^\"']*sprow[^\"']*[\"'][^>]*>(?<content>[\\s\\S]*?)</div>", RegexOptions.IgnoreCase))
        {
            var content = match.Groups["content"].Value;
            var labelMatch = Regex.Match(content, "<span[^>]*class=[\"'][^\"']*sptitle[^\"']*[\"'][^>]*>(?<label>[\\s\\S]*?)</span>", RegexOptions.IgnoreCase);
            if (!labelMatch.Success) continue;
            var valueRaw = content.Replace(labelMatch.Value, " ", StringComparison.Ordinal);
            TryAddSpec(specs, labelMatch.Groups["label"].Value, valueRaw);
        }

        return specs;
    }

    private static string CleanHtmlText(string value)
    {
        value = Regex.Replace(value, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        var text = Regex.Replace(value, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();
        return text;
    }

    private static string? GetSpec(Dictionary<string, string> specs, string label) =>
        specs.TryGetValue(label, out var value) ? value : null;

    private static string? GetSpecAny(Dictionary<string, string> specs, params string[] labels)
    {
        foreach (var label in labels)
            if (specs.TryGetValue(label, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }

    private static string? ExtractFact(string text, string startLabel, string endLabel)
    {
        var start = text.IndexOf(startLabel, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += startLabel.Length;
        var end = text.IndexOf(endLabel, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) end = Math.Min(text.Length, start + 120);
        return SanitizeExtractedFact(text[start..end].Trim().Trim('-', ':'));
    }

    private static string? ExtractFactByNextLabels(string text, string startLabel, params string[] possibleEndLabels)
    {
        var start = text.IndexOf(startLabel, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += startLabel.Length;
        var end = text.Length;
        foreach (var endLabel in possibleEndLabels)
        {
            var candidate = text.IndexOf(endLabel, start, StringComparison.OrdinalIgnoreCase);
            if (candidate >= 0 && candidate < end) end = candidate;
        }
        if (end == text.Length) end = Math.Min(text.Length, start + 120);
        return SanitizeExtractedFact(text[start..end].Trim().Trim('-', ':'));
    }

    private static string? SanitizeExtractedFact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        var contaminationMarkers = new[] { "\"@type\"", "\"inLanguage\"", "\"itemListElement\"", "\"mainEntity\"", "\"url\"", "https://", "http://" };
        if (contaminationMarkers.Any(marker => cleaned.Contains(marker, StringComparison.OrdinalIgnoreCase))) return null;
        if (cleaned.Length > 120) return null;
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static int? ParseMileageFromText(string text)
    {
        var milMatch = Regex.Match(text, @"([0-9][0-9\s]*)\s*\bmil\b", RegexOptions.IgnoreCase);
        if (milMatch.Success)
        {
            var digits = milMatch.Groups[1].Value.Replace(" ", "");
            if (int.TryParse(digits, out var mileageMil))
                return mileageMil * 10;
        }

        var kmMatch = Regex.Match(text, @"([0-9][0-9\s,]*)\s*km", RegexOptions.IgnoreCase);
        if (kmMatch.Success)
        {
            var digits = kmMatch.Groups[1].Value.Replace(" ", "").Replace(",", "");
            if (int.TryParse(digits, out var mileageKm))
                return mileageKm;
        }

        return null;
    }
}
