using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabDB = Autodesk.Fabrication.DB.Database;
using CADapp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace FabricationSample.Services.Bridge
{
    /// <summary>
    /// Exposes the live Fabrication database as a local REST API on localhost:5050.
    ///
    /// Endpoints:
    ///   GET  /api/status
    ///   GET  /api/cache
    ///   GET  /api/cache/refresh
    ///   GET  /api/image?path=                     — serve PNG/image file from disk
    ///   GET  /api/products?q=&manufacturer=&material=&install_type=&group=&specification=&size=&limit=&offset=
    ///   GET  /api/products/count?...
    ///   GET  /api/products/{id}                   — single product with prices + install times
    ///   GET  /api/services
    ///   GET  /api/services/items?service=&limit=&offset=
    ///   GET  /api/service-templates/{name}/tree   — full template hierarchy (tabs→buttons→items→conditions)
    ///   GET  /api/price-lists?supplier_group=&limit=
    ///   GET  /api/price-lists/entries?supplier_group=&list_name=&q=&limit=&offset=
    ///   POST /api/price-lists/entries             — add/update a price entry (JSON body)
    ///   GET  /api/install-times?limit=
    ///   GET  /api/install-times/entries?table_name=&group=&q=&limit=&offset=
    ///   POST /api/install-times/entries           — add/update an install entry (JSON body)
    ///   GET  /api/images/map?limit=
    ///   GET  /api/materials                       — all materials
    ///   GET  /api/sections                        — all sections
    ///   GET  /api/specifications                  — all specifications
    ///   GET  /api/job/items?service=&status=&section=&limit=&offset= — placed job items
    ///   GET  /api/job/items/{uniqueId}            — single job item detail
    ///   PUT  /api/products/{id}/supplier-ids      — update supplier IDs (JSON body)
    ///   POST /api/products/harrison-codes/import  — bulk Harrison code import (TSV body)
    /// </summary>
    public class FabricationBridgeService : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        private Thread _listenerThread;
        private volatile bool _running;

        public const string ListenPrefix = "http://localhost:5050/";

        // ── Cache ────────────────────────────────────────────────────────────────
        // NOTE: All ID-keyed dictionaries use OrdinalIgnoreCase because the HTTP path is
        //       lowercased for routing (ToLowerInvariant) but product IDs are mixed-case.

        private static Dictionary<string, List<Dict>> _priceCache        = new Dictionary<string, List<Dict>>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, List<Dict>> _installCache       = new Dictionary<string, List<Dict>>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string>                _productListedIds   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static List<Dict>                     _serviceItemsList   = new List<Dict>();
        private static List<Dict>                     _allProductsList    = new List<Dict>();
        private static Dictionary<string, Dict>       _productIndex       = new Dictionary<string, Dict>(StringComparer.OrdinalIgnoreCase);
        // Flattened price list entries enriched with product info (description, harrison_code, etc.)
        private static List<Dict>                     _priceEntriesList   = new List<Dict>();
        // Flattened install time entries enriched with product info
        private static List<Dict>                     _installEntriesList = new List<Dict>();
        // Product ID → image file path (one image per product, first found from service items)
        private static Dictionary<string, string>     _productImageMap    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Job items (placed items in current drawing) — rebuilt each cache cycle
        private static List<Dict>                     _jobItemsList       = new List<Dict>();
        private static Dictionary<string, Dict>       _jobItemIndex       = new Dictionary<string, Dict>(StringComparer.OrdinalIgnoreCase);

        private static volatile bool _cacheReady    = false;
        private static volatile bool _cacheBuilding = false;
        private static string        _cacheError    = null;
        private static DateTime?     _cacheBuiltAt  = null;

        public FabricationBridgeService()
        {
            _listener.Prefixes.Add(ListenPrefix);
        }

        public void Start()
        {
            try
            {
                _listener.Start();
                _running = true;
                _listenerThread = new Thread(Listen) { IsBackground = true, Name = "FabricationBridge" };
                _listenerThread.Start();
                WriteMessage($"[FabBridge] Listening on {ListenPrefix}");
                StartCacheBuild();
            }
            catch (HttpListenerException ex) { WriteMessage($"[FabBridge] Failed to start: {ex.Message}"); }
            catch (Exception ex)             { WriteMessage($"[FabBridge] Start error: {ex.Message}"); }
        }

        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
            WriteMessage("[FabBridge] Stopped.");
        }

        public void Dispose() => Stop();

        // ── Cache build ───────────────────────────────────────────────────────────

        private static void StartCacheBuild()
        {
            if (_cacheBuilding) return;
            _cacheBuilding = true;
            _cacheReady    = false;
            _cacheError    = null;
            new Thread(BuildCache) { IsBackground = true, Name = "FabBridgeCache" }.Start();
        }

        private static void BuildCache()
        {
            try
            {
                WriteMessage("[FabBridge] Cache build started...");

                // Phase 1: Price lists  (mirrors ProductInfoExportService.CollectPriceLists)
                WriteMessage("[FabBridge] Phase 1: Price lists...");
                var priceCacheNew = new Dictionary<string, List<Dict>>(StringComparer.OrdinalIgnoreCase);
                SafeRead(() =>
                {
                    foreach (var sg in FabDB.SupplierGroups)
                    {
                        string groupName = sg.Name ?? "";
                        foreach (var list in sg.PriceLists)
                        {
                            if (!(list is PriceList pl)) continue;
                            string listName = pl.Name ?? "";
                            foreach (var entry in pl.Products)
                            {
                                string id = entry.DatabaseId;
                                if (string.IsNullOrEmpty(id) || id == "N/A") continue;
                                string status = entry.Status == ProductEntryStatus.Active ? "Active" :
                                    entry.Status == ProductEntryStatus.PriceOnApplication ? "POA" : "Discon";
                                var pe = new Dict
                                {
                                    ["supplier_group"] = groupName,
                                    ["list_name"]      = listName,
                                    ["cost"]           = entry.Value,
                                    ["discount_code"]  = entry.DiscountCode ?? "",
                                    ["units"]          = entry.CostedByLength ? "per(ft)" : "(each)",
                                    ["date"]           = entry.Date.HasValue ? entry.Date.Value.ToString("yyyy-MM-dd") : "",
                                    ["status"]         = status,
                                };
                                if (!priceCacheNew.ContainsKey(id))
                                    priceCacheNew[id] = new List<Dict>();
                                priceCacheNew[id].Add(pe);
                            }
                        }
                    }
                });

                // Phase 2: Install times  (mirrors CollectInstallationTimes)
                WriteMessage("[FabBridge] Phase 2: Install times...");
                var installCacheNew = new Dictionary<string, List<Dict>>(StringComparer.OrdinalIgnoreCase);
                SafeRead(() =>
                {
                    var tables = FabDB.InstallationTimesTable;
                    if (tables == null) return;
                    foreach (var table in tables)
                    {
                        if (!(table is InstallationTimesTable simple)) continue;
                        string tableName  = table.Name  ?? "";
                        string tableGroup = table.Group ?? "";
                        foreach (var entry in simple.Products)
                        {
                            string id = entry.DatabaseId;
                            if (string.IsNullOrEmpty(id) || id == "N/A") continue;
                            string status = entry.Status == ProductEntryStatus.Active ? "Active" :
                                entry.Status == ProductEntryStatus.PriceOnApplication ? "POA" : "Discon";
                            var ie = new Dict
                            {
                                ["table_name"] = tableName,
                                ["group"]      = tableGroup,
                                ["labor_rate"] = entry.Value,
                                ["units"]      = entry.CostedByLength ? "per(ft)" : "(each)",
                                ["status"]     = status,
                            };
                            if (!installCacheNew.ContainsKey(id))
                                installCacheNew[id] = new List<Dict>();
                            installCacheNew[id].Add(ie);
                        }
                    }
                });

                // Phase 3: Service items + product-listed scan  (mirrors ItemDataExportService)
                // Two-pass strategy:
                //   Pass 1 — Enumerate templates (via Database.ServiceTemplates) and cache
                //            processed button/item data per template name. This is the expensive
                //            pass (ContentManager.LoadItem for each button item).
                //   Pass 2 — Iterate FabDB.Services and clone cached template items with each
                //            service's name. Cheap pass (Dict copy only).
                // This fixes the issue where most services returned null for
                // ServiceTemplate.ServiceTabs when accessed through the Service object.
                WriteMessage("[FabBridge] Phase 3: Service items and product lists...");
                var productListedNew  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var serviceItemsNew   = new List<Dict>();
                var productImageMapNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // product_id → first PNG path found

                // Pass 1: Build template cache — templateName → List<Dict>
                var templateItemCache = new Dictionary<string, List<Dict>>(StringComparer.OrdinalIgnoreCase);
                SafeRead(() =>
                {
                    // Primary source: Database.ServiceTemplates (direct template enumeration)
                    IEnumerable<ServiceTemplate> templates = null;
                    try { templates = FabDB.ServiceTemplates?.ToList(); }
                    catch { templates = null; }

                    // Fallback: extract unique templates from services
                    if (templates == null || !templates.Any())
                    {
                        WriteMessage("[FabBridge] Phase 3: ServiceTemplates unavailable, falling back to per-service templates.");
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var fromSvc = new List<ServiceTemplate>();
                        foreach (var svc in FabDB.Services)
                        {
                            try
                            {
                                var t = svc.ServiceTemplate;
                                if (t == null) continue;
                                string n = t.Name ?? "";
                                if (!string.IsNullOrEmpty(n) && seen.Add(n))
                                    fromSvc.Add(t);
                            }
                            catch { }
                        }
                        templates = fromSvc;
                    }

                    int tmplCount = 0;
                    foreach (var tmpl in templates)
                    {
                        try
                        {
                            string tmplName = FixEncoding(tmpl.Name ?? "");
                            if (string.IsNullOrEmpty(tmplName) || templateItemCache.ContainsKey(tmplName)) continue;
                            if (tmpl.ServiceTabs == null) continue;

                            var tmplItems = new List<Dict>();
                            foreach (var tab in tmpl.ServiceTabs)
                            {
                                if (tab.ServiceButtons == null) continue;
                                foreach (var btn in tab.ServiceButtons)
                                {
                                    string btnName = btn.Name ?? "";
                                    if (btn.ServiceButtonItems == null) continue;
                                    foreach (var sbItem in btn.ServiceButtonItems)
                                    {
                                        try
                                        {
                                            var item     = ContentManager.LoadItem(sbItem.ItemPath);
                                            string itemPath = item?.FilePath ?? "";

                                            // Image source priority:
                                            // 1. Fabrication API Item.ImagePath property
                                            // 2. PNG file with same name alongside the .ITM file
                                            // 3. ServiceButton.GetButtonImageFilename()
                                            string imagePath = "";
                                            try
                                            {
                                                if (item != null && !string.IsNullOrEmpty(item.ImagePath)
                                                    && File.Exists(item.ImagePath))
                                                    imagePath = item.ImagePath;
                                            }
                                            catch { }

                                            if (string.IsNullOrEmpty(imagePath) && !string.IsNullOrEmpty(itemPath))
                                            {
                                                string png = Path.ChangeExtension(itemPath, ".png");
                                                if (!File.Exists(png)) png = Path.ChangeExtension(itemPath, ".PNG");
                                                if (File.Exists(png)) imagePath = png;
                                            }

                                            // Service button image (button-level, not item-level)
                                            string buttonImagePath = "";
                                            try
                                            {
                                                string btnImg = btn.GetButtonImageFilename();
                                                if (!string.IsNullOrEmpty(btnImg) && File.Exists(btnImg))
                                                    buttonImagePath = btnImg;
                                            }
                                            catch { }

                                            // Fall back to button image if no item image found
                                            if (string.IsNullOrEmpty(imagePath) && !string.IsNullOrEmpty(buttonImagePath))
                                                imagePath = buttonImagePath;

                                            var cond     = sbItem.ServiceTemplateCondition;
                                            string condDesc = cond?.Description ?? "";
                                            string gt    = cond != null ? (cond.GreaterThan > -1      ? cond.GreaterThan.ToString()      : "Unrestricted") : "N/A";
                                            string condId = cond != null ? cond.Id.ToString() : "N/A";
                                            string lte   = cond != null ? (cond.LessThanEqualTo > -1  ? cond.LessThanEqualTo.ToString()  : "Unrestricted") : "N/A";

                                            if (item?.ProductList?.Rows != null)
                                            {
                                                foreach (var row in item.ProductList.Rows)
                                                {
                                                    string entryName = "";
                                                    try { entryName = row.Name ?? ""; } catch { }
                                                    if (!string.IsNullOrEmpty(entryName))
                                                    {
                                                        productListedNew.Add(entryName);
                                                        if (!string.IsNullOrEmpty(imagePath) && !productImageMapNew.ContainsKey(entryName))
                                                            productImageMapNew[entryName] = imagePath;
                                                    }
                                                    tmplItems.Add(new Dict
                                                    {
                                                        ["service_name"]   = "",
                                                        ["template_name"]  = tmplName,
                                                        ["button_name"]    = btnName,
                                                        ["item_path"]      = itemPath,
                                                        ["image_path"]     = imagePath,
                                                        ["button_image"]   = buttonImagePath,
                                                        ["entry_name"]     = entryName,
                                                        ["condition_desc"] = condDesc,
                                                        ["greater_than"]   = gt,
                                                        ["condition_id"]   = condId,
                                                        ["less_than_eq"]   = lte,
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                tmplItems.Add(new Dict
                                                {
                                                    ["service_name"]   = "",
                                                    ["template_name"]  = tmplName,
                                                    ["button_name"]    = btnName,
                                                    ["item_path"]      = itemPath,
                                                    ["image_path"]     = imagePath,
                                                    ["button_image"]   = buttonImagePath,
                                                    ["entry_name"]     = "",
                                                    ["condition_desc"] = condDesc,
                                                    ["greater_than"]   = gt,
                                                    ["condition_id"]   = condId,
                                                    ["less_than_eq"]   = lte,
                                                });
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }

                            if (tmplItems.Count > 0)
                            {
                                templateItemCache[tmplName] = tmplItems;
                                tmplCount++;
                            }
                        }
                        catch { }
                    }
                    WriteMessage($"[FabBridge] Phase 3a: Cached {tmplCount} templates ({templateItemCache.Values.Sum(l => l.Count):N0} item rows).");
                });

                // Pass 2: Map services → cached template items
                SafeRead(() =>
                {
                    int mappedServices = 0;
                    foreach (var svc in FabDB.Services)
                    {
                        try
                        {
                            string svcName = FixEncoding(svc.Name ?? "");
                            string tmplName = FixEncoding(svc.ServiceTemplate?.Name ?? "");
                            if (string.IsNullOrEmpty(tmplName)) continue;

                            if (!templateItemCache.TryGetValue(tmplName, out var tmplItems)) continue;

                            foreach (var tmplItem in tmplItems)
                            {
                                var clone = new Dict(tmplItem);
                                clone["service_name"] = svcName;
                                serviceItemsNew.Add(clone);
                            }
                            mappedServices++;
                        }
                        catch { }
                    }
                    WriteMessage($"[FabBridge] Phase 3b: Mapped {mappedServices} services → {serviceItemsNew.Count:N0} service item rows.");
                });

                // Phase 4: All products with supplier IDs + is_product_listed + product index
                WriteMessage("[FabBridge] Phase 4: All products...");
                var allProductsNew = new List<Dict>();
                var productIndexNew = new Dictionary<string, Dict>(StringComparer.OrdinalIgnoreCase);
                SafeRead(() =>
                {
                    foreach (var pd in ProductDatabase.ProductDefinitions)
                    {
                        if (string.IsNullOrEmpty(pd.Id) || pd.Id == "N/A") continue;
                        var d = ProductDefToDict(pd, includeSupplierIds: true);
                        d["is_product_listed"] = productListedNew.Contains(pd.Id) ? "Yes" : "No";
                        allProductsNew.Add(d);
                        productIndexNew[pd.Id] = d;
                    }
                });

                // Phase 5: Flatten price entries enriched with product info + harrison_code
                WriteMessage("[FabBridge] Phase 5: Enriching price list entries...");
                var priceEntriesNew = new List<Dict>();
                foreach (var kvp in priceCacheNew)
                {
                    string productId = kvp.Key;
                    productIndexNew.TryGetValue(productId, out var productInfo);
                    string description  = productInfo != null ? Str(productInfo, "description")  : "";
                    string manufacturer = productInfo != null ? Str(productInfo, "manufacturer") : "";
                    string material     = productInfo != null ? Str(productInfo, "material")     : "";
                    string size         = productInfo != null ? Str(productInfo, "size")         : "";
                    string harrisonCode = "";
                    if (productInfo != null && productInfo.TryGetValue("supplier_ids", out var sidsObj)
                        && sidsObj is Dict sids)
                    {
                        // Look for Harrison key (case-insensitive)
                        foreach (var kv in sids)
                        {
                            if (kv.Key.IndexOf("Harrison", StringComparison.OrdinalIgnoreCase) >= 0)
                            { harrisonCode = kv.Value?.ToString() ?? ""; break; }
                        }
                    }

                    foreach (var pe in kvp.Value)
                    {
                        var entry = new Dict();
                        foreach (var kv in pe) entry[kv.Key] = kv.Value;
                        entry["product_id"]    = productId;
                        entry["description"]   = description;
                        entry["manufacturer"]  = manufacturer;
                        entry["material"]      = material;
                        entry["size"]          = size;
                        entry["harrison_code"] = harrisonCode;
                        priceEntriesNew.Add(entry);
                    }
                }

                // Phase 6: Flatten install time entries enriched with product info
                WriteMessage("[FabBridge] Phase 6: Enriching install time entries...");
                var installEntriesNew = new List<Dict>();
                foreach (var kvp in installCacheNew)
                {
                    string productId = kvp.Key;
                    productIndexNew.TryGetValue(productId, out var productInfoIt);
                    string description2  = productInfoIt != null ? Str(productInfoIt, "description")  : "";
                    string manufacturer2 = productInfoIt != null ? Str(productInfoIt, "manufacturer") : "";
                    string material2     = productInfoIt != null ? Str(productInfoIt, "material")     : "";
                    string size2         = productInfoIt != null ? Str(productInfoIt, "size")         : "";
                    string harrisonCode2 = "";
                    if (productInfoIt != null && productInfoIt.TryGetValue("supplier_ids", out var sidsObj2)
                        && sidsObj2 is Dict sids2)
                    {
                        foreach (var kv in sids2)
                        {
                            if (kv.Key.IndexOf("Harrison", StringComparison.OrdinalIgnoreCase) >= 0)
                            { harrisonCode2 = kv.Value?.ToString() ?? ""; break; }
                        }
                    }
                    foreach (var ie in kvp.Value)
                    {
                        var entry = new Dict();
                        foreach (var kv in ie) entry[kv.Key] = kv.Value;
                        entry["product_id"]    = productId;
                        entry["description"]   = description2;
                        entry["manufacturer"]  = manufacturer2;
                        entry["material"]      = material2;
                        entry["size"]          = size2;
                        entry["harrison_code"] = harrisonCode2;
                        installEntriesNew.Add(entry);
                    }
                }

                // Phase 7: Job items (placed items in current drawing)
                WriteMessage("[FabBridge] Phase 7: Job items...");
                var jobItemsNew = new List<Dict>();
                var jobItemIndexNew = new Dictionary<string, Dict>(StringComparer.OrdinalIgnoreCase);
                SafeRead(() =>
                {
                    try
                    {
                        var items = Job.Items;
                        if (items == null) return;
                        foreach (var item in items)
                        {
                            try
                            {
                                string uid = item.UniqueId ?? "";
                                if (string.IsNullOrEmpty(uid)) continue;

                                // Collect dimensions
                                var dims = new Dict();
                                try
                                {
                                    if (item.Dimensions != null)
                                    {
                                        for (int di = 0; di < item.Dimensions.Count; di++)
                                        {
                                            var dim = item.Dimensions[di];
                                            if (dim != null && !string.IsNullOrEmpty(dim.Name))
                                                dims[dim.Name] = dim.Value;
                                        }
                                    }
                                }
                                catch { }

                                // Collect custom data
                                var cdata = new Dict();
                                try
                                {
                                    if (item.CustomData != null)
                                    {
                                        foreach (var cd in item.CustomData)
                                        {
                                            if (cd?.Data == null) continue;
                                            string cdName = cd.Data.Description ?? ("CD" + cd.Data.Id);
                                            if (cd is CustomDataStringValue sv)
                                                cdata[cdName] = sv.Value ?? "";
                                            else if (cd is CustomDataIntegerValue iv)
                                                cdata[cdName] = iv.Value.ToString();
                                            else if (cd is CustomDataDoubleValue dv)
                                                cdata[cdName] = dv.Value.ToString();
                                        }
                                    }
                                }
                                catch { }

                                // Collect connector info
                                var connectors = new List<Dict>();
                                try
                                {
                                    if (item.Connectors != null)
                                    {
                                        for (int ci = 0; ci < item.Connectors.Count; ci++)
                                        {
                                            var conn = item.Connectors[ci];
                                            if (conn == null) continue;
                                            connectors.Add(new Dict
                                            {
                                                ["index"]     = ci,
                                                ["is_locked"] = conn.IsLocked,
                                            });
                                        }
                                    }
                                }
                                catch { }

                                var d = new Dict
                                {
                                    ["unique_id"]      = uid,
                                    ["name"]           = item.Name ?? "",
                                    ["cid"]            = item.CID,
                                    ["pattern_number"] = item.PatternNumber,
                                    ["status"]         = item.Status?.Name ?? "",
                                    ["section"]        = item.Section?.Description ?? "",
                                    ["service"]        = item.Service?.Name ?? "",
                                    ["notes"]          = item.Notes ?? "",
                                    ["order"]          = item.Order ?? "",
                                    ["zone"]           = item.Zone ?? "",
                                    ["spool_name"]     = item.SpoolName ?? "",
                                    ["dimensions"]     = dims,
                                    ["custom_data"]    = cdata,
                                    ["connector_count"]= connectors.Count,
                                    ["connectors"]     = connectors,
                                };
                                jobItemsNew.Add(d);
                                jobItemIndexNew[uid] = d;
                            }
                            catch { }
                        }
                    }
                    catch { }
                });
                WriteMessage($"[FabBridge] Phase 7: {jobItemsNew.Count} job items found.");

                // Atomically swap caches
                _priceCache          = priceCacheNew;
                _installCache        = installCacheNew;
                _productListedIds    = productListedNew;
                _serviceItemsList    = serviceItemsNew;
                _allProductsList     = allProductsNew;
                _productIndex        = productIndexNew;
                _priceEntriesList    = priceEntriesNew;
                _installEntriesList  = installEntriesNew;
                _productImageMap     = productImageMapNew;
                _jobItemsList        = jobItemsNew;
                _jobItemIndex        = jobItemIndexNew;
                _cacheBuiltAt        = DateTime.UtcNow;
                _cacheReady          = true;
                _cacheBuilding       = false;

                WriteMessage($"[FabBridge] Cache ready: {allProductsNew.Count:N0} products, " +
                    $"{priceEntriesNew.Count:N0} price entries ({priceCacheNew.Count:N0} priced), " +
                    $"{installEntriesNew.Count:N0} install entries ({installCacheNew.Count:N0} timed), " +
                    $"{productImageMapNew.Count:N0} product images, " +
                    $"{serviceItemsNew.Count:N0} service item rows.");
            }
            catch (Exception ex)
            {
                _cacheError    = ex.Message;
                _cacheBuilding = false;
                WriteMessage($"[FabBridge] Cache build error: {ex.Message}");
            }
        }

        // ── Listener loop ─────────────────────────────────────────────────────────

        private void Listen()
        {
            while (_running)
            {
                try   { var ctx = _listener.GetContext(); ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx)); }
                catch (HttpListenerException) { break; }
                catch { }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                string path   = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
                var    query  = req.QueryString;
                string method = req.HttpMethod;

                // CORS preflight
                if (method == "OPTIONS")
                {
                    resp.StatusCode = 204;
                    resp.Close();
                    return;
                }

                // Binary image endpoint — handled separately (not JSON)
                if (method == "GET" && path == "/api/image")
                {
                    HandleImage(query, resp);
                    return;
                }

                string json;
                int    status = 200;

                if      (method == "GET" && path == "/api/status")
                    json = HandleStatus();
                else if (method == "GET" && path == "/api/cache")
                    json = HandleCacheStatus();
                else if ((method == "GET" || method == "POST") && path == "/api/cache/refresh")
                    json = HandleRefreshCache();
                else if (method == "GET" && path == "/api/products")
                    json = HandleSearchProducts(query);
                else if (method == "GET" && path == "/api/products/count")
                    json = HandleProductCount(query);
                // PUT /api/products/{id}/supplier-ids
                else if (method == "PUT" && path.EndsWith("/supplier-ids") && path.StartsWith("/api/products/"))
                    json = HandleUpdateSupplierIds(path, ReadBody(req));
                else if (method == "GET" && path.StartsWith("/api/products/"))
                    json = HandleGetProduct(path.Substring("/api/products/".Length));
                else if (method == "GET" && path == "/api/services")
                    json = HandleGetServices();
                else if (method == "GET" && path == "/api/services/items")
                    json = HandleGetServiceItems(query);
                // GET /api/service-templates/{name}/tree
                else if (method == "GET" && path.StartsWith("/api/service-templates/") && path.EndsWith("/tree"))
                    json = HandleGetServiceTemplateTree(path);
                // POST /api/price-lists/entries — add/update
                else if (method == "POST" && path == "/api/price-lists/entries")
                    json = HandleAddPriceEntry(ReadBody(req));
                else if (method == "GET" && path == "/api/price-lists/entries")
                    json = HandlePriceListEntries(query);
                else if (method == "GET" && path == "/api/price-lists")
                    json = HandleGetPriceLists(query);
                // POST /api/install-times/entries — add/update
                else if (method == "POST" && path == "/api/install-times/entries")
                    json = HandleAddInstallEntry(ReadBody(req));
                else if (method == "GET" && path == "/api/install-times/entries")
                    json = HandleInstallTimeEntries(query);
                else if (method == "GET" && path == "/api/install-times")
                    json = HandleGetInstallTimes(query);
                else if (method == "GET" && path == "/api/images/map")
                    json = HandleImagesMap(query);
                // New read-only endpoints
                else if (method == "GET" && path == "/api/materials")
                    json = HandleGetMaterials();
                else if (method == "GET" && path == "/api/sections")
                    json = HandleGetSections();
                else if (method == "GET" && path == "/api/specifications")
                    json = HandleGetSpecifications();
                else if (method == "GET" && path.StartsWith("/api/job/items/"))
                    json = HandleGetJobItem(path.Substring("/api/job/items/".Length));
                else if (method == "GET" && path == "/api/job/items")
                    json = HandleGetJobItems(query);
                // DELETE /api/price-lists/entries?list_name=X&product_id=Y
                else if (method == "DELETE" && path == "/api/price-lists/entries")
                    json = HandleDeletePriceEntry(query);
                // DELETE /api/install-times/entries?table_name=X&product_id=Y
                else if (method == "DELETE" && path == "/api/install-times/entries")
                    json = HandleDeleteInstallEntry(query);
                // POST /api/job/items/{uniqueId}/swap
                else if (method == "POST" && path.StartsWith("/api/job/items/") && path.EndsWith("/swap"))
                    json = HandleSwapItem(path, ReadBody(req));
                // POST /api/job/items/undo
                else if (method == "POST" && path == "/api/job/items/undo")
                    json = HandleUndoSwap();
                // POST /api/cache/export — trigger ProductInfo CSV export
                else if (method == "POST" && path == "/api/cache/export")
                    json = HandleCacheExport(ReadBody(req));
                // POST /api/products/harrison-codes/import — bulk Harrison code import
                else if (method == "POST" && path == "/api/products/harrison-codes/import")
                    json = HandleHarrisonImport(ReadBody(req));
                else { status = 404; json = JsonError("endpoint not found"); }

                byte[] body = Encoding.UTF8.GetBytes(json);
                resp.StatusCode      = status;
                resp.ContentType     = "application/json; charset=utf-8";
                resp.ContentLength64 = body.Length;
                resp.OutputStream.Write(body, 0, body.Length);
            }
            catch (Exception ex)
            {
                try
                {
                    resp.StatusCode = 500;
                    byte[] err = Encoding.UTF8.GetBytes(JsonError(ex.Message));
                    resp.ContentType     = "application/json; charset=utf-8";
                    resp.ContentLength64 = err.Length;
                    resp.OutputStream.Write(err, 0, err.Length);
                }
                catch { }
            }
            finally { try { resp.Close(); } catch { } }
        }

        // ── Route handlers ────────────────────────────────────────────────────────

        private string HandleStatus()
        {
            int  productCount       = 0;
            int  serviceCount       = 0;
            int  supplierGroupCount = 0;
            bool dbLoaded           = false;
            string databasePath     = "";
            string databaseName     = "";
            string profileName      = "";
            SafeRead(() =>
            {
                productCount       = ProductDatabase.ProductDefinitions.Count();
                serviceCount       = FabDB.Services.Count();
                supplierGroupCount = FabDB.SupplierGroups.Count();
                dbLoaded           = true;
                try
                {
                    databasePath = Autodesk.Fabrication.ApplicationServices.Application.DatabasePath ?? "";
                    databaseName = string.IsNullOrEmpty(databasePath) ? "" : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(databasePath.TrimEnd('\\', '/')));
                    profileName  = Autodesk.Fabrication.ApplicationServices.Application.CurrentProfile ?? "";
                }
                catch { }
            });

            // Product-level counts from cache (N/A-aware — N/A is never a valid value)
            // Return null (not 0) when cache is still building, so consumers can
            // distinguish "not yet counted" from "counted and zero."
            // Count only products that:
            //   1. Exist in _productIndex (are real products in the product editor, not N/A items)
            //   2. Have at least one entry with value > 0 (excludes zero-cost/zero-labor entries)
            object productsWithCost    = null;
            object productsWithLabor   = null;
            object productsWithHarrison = null;
            if (_cacheReady)
            {
                int costCount = 0;
                foreach (var kvp in _priceCache)
                {
                    if (!_productIndex.ContainsKey(kvp.Key)) continue; // skip N/A items
                    foreach (var pe in kvp.Value)
                    {
                        if (pe.TryGetValue("cost", out var cv) && cv is double cd && cd > 0)
                        { costCount++; break; }
                    }
                }
                productsWithCost = costCount;

                int laborCount = 0;
                foreach (var kvp in _installCache)
                {
                    if (!_productIndex.ContainsKey(kvp.Key)) continue; // skip N/A items
                    foreach (var ie in kvp.Value)
                    {
                        if (ie.TryGetValue("labor_rate", out var lv) && lv is double ld && ld > 0)
                        { laborCount++; break; }
                    }
                }
                productsWithLabor = laborCount;

                int hCount = 0;
                foreach (var pd in _allProductsList)
                {
                    if (pd.TryGetValue("supplier_ids", out var sObj) && sObj is Dict sids)
                    {
                        foreach (var kv in sids)
                        {
                            if (kv.Key.IndexOf("Harrison", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string hv = kv.Value?.ToString() ?? "";
                                if (hv.Length > 0 && !hv.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                                    hCount++;
                                break;
                            }
                        }
                    }
                }
                productsWithHarrison = hCount;
            }

            return Serialize(new Dict
            {
                ["status"]               = "ok",
                ["db_loaded"]            = dbLoaded,
                ["database_path"]        = databasePath,
                ["database_name"]        = databaseName,
                ["profile_name"]         = profileName,
                ["product_count"]        = productCount,
                ["service_count"]        = serviceCount,
                ["supplier_group_count"] = supplierGroupCount,
                ["timestamp"]            = DateTime.UtcNow.ToString("o"),
                ["listen_url"]           = ListenPrefix,
                ["cache_ready"]          = _cacheReady,
                ["cache_building"]       = _cacheBuilding,
                ["cache_product_count"]  = _allProductsList.Count,
                ["products_with_cost"]    = productsWithCost,
                ["products_with_labor"]   = productsWithLabor,
                ["products_with_harrison"]= productsWithHarrison,
                ["image_count"]          = _productImageMap.Count,
                ["price_entries_count"]  = _priceEntriesList.Count,
                ["install_entries_count"]= _installEntriesList.Count,
            });
        }

        private string HandleCacheStatus()
        {
            int total  = _allProductsList.Count;
            // Count only real products (in _productIndex) where at least one entry has value > 0
            int priced = 0;
            foreach (var kvp in _priceCache)
            {
                if (!_productIndex.ContainsKey(kvp.Key)) continue; // skip N/A items
                foreach (var pe in kvp.Value)
                {
                    if (pe.TryGetValue("cost", out var cv) && cv is double cd && cd > 0)
                    { priced++; break; }
                }
            }
            int timed = 0;
            foreach (var kvp in _installCache)
            {
                if (!_productIndex.ContainsKey(kvp.Key)) continue; // skip N/A items
                foreach (var ie in kvp.Value)
                {
                    if (ie.TryGetValue("labor_rate", out var lv) && lv is double ld && ld > 0)
                    { timed++; break; }
                }
            }
            string databasePath = "";
            string databaseName = "";
            SafeRead(() =>
            {
                try
                {
                    databasePath = Autodesk.Fabrication.ApplicationServices.Application.DatabasePath ?? "";
                    databaseName = string.IsNullOrEmpty(databasePath) ? "" : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(databasePath.TrimEnd('\\', '/')));
                }
                catch { }
            });
            return Serialize(new Dict
            {
                ["database_path"]        = databasePath,
                ["database_name"]        = databaseName,
                ["cache_ready"]          = _cacheReady,
                ["cache_building"]       = _cacheBuilding,
                ["cache_error"]          = _cacheError ?? "",
                ["product_count"]        = total,
                ["service_items_count"]  = _serviceItemsList.Count,
                ["products_with_cost"]   = priced,
                ["products_with_cost_pct"] = total > 0 ? Math.Round(priced * 100.0 / total, 1) : 0.0,
                ["products_with_labor"]  = timed,
                ["products_with_labor_pct"] = total > 0 ? Math.Round(timed  * 100.0 / total, 1) : 0.0,
                ["product_listed_count"] = _productListedIds.Count,
                ["price_entries_count"]   = _priceEntriesList.Count,
                ["install_entries_count"] = _installEntriesList.Count,
                ["image_count"]           = _productImageMap.Count,
                ["job_items_count"]       = _jobItemsList.Count,
                ["built_at"]              = _cacheBuiltAt.HasValue ? _cacheBuiltAt.Value.ToString("o") : "",
            });
        }

        private string HandleRefreshCache()
        {
            if (_cacheBuilding) return Serialize(new Dict { ["status"] = "already_building" });
            StartCacheBuild();
            return Serialize(new Dict { ["status"] = "started" });
        }

        private string HandleImage(NameValueCollection q, HttpListenerResponse resp)
        {
            string filePath = q["path"] ?? "";
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // Security: only serve image files, reject path traversal
            if (string.IsNullOrEmpty(filePath) || (ext != ".png" && ext != ".jpg" && ext != ".bmp"))
            {
                Write404(resp, "invalid image path");
                return null;
            }
            if (filePath.Contains("..") || filePath.Contains("%"))
            {
                Write404(resp, "invalid path");
                return null;
            }

            if (!File.Exists(filePath)) { Write404(resp, "image not found"); return null; }

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                resp.StatusCode      = 200;
                resp.ContentType     = ext == ".png" ? "image/png" : ext == ".jpg" ? "image/jpeg" : "image/bmp";
                resp.ContentLength64 = bytes.Length;
                resp.Headers.Add("Cache-Control", "max-age=3600");
                resp.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch { Write404(resp, "read error"); }
            finally { try { resp.Close(); } catch { } }
            return null; // response already written
        }

        private static void Write404(HttpListenerResponse resp, string msg)
        {
            resp.StatusCode  = 404;
            byte[] err = Encoding.UTF8.GetBytes(JsonError(msg));
            resp.ContentType     = "application/json; charset=utf-8";
            resp.ContentLength64 = err.Length;
            resp.OutputStream.Write(err, 0, err.Length);
            try { resp.Close(); } catch { }
        }

        private string HandleSearchProducts(NameValueCollection q)
        {
            string srch    = (q["q"]             ?? "").ToLower();
            string mfr     = (q["manufacturer"]  ?? "").ToLower();
            string mat     = (q["material"]       ?? "").ToLower();
            string install = (q["install_type"]   ?? "").ToLower();
            string grp     = (q["group"]          ?? "").ToLower();
            string spec    = (q["specification"]  ?? "").ToLower();
            string size    = (q["size"]           ?? "").ToLower();
            int    limit   = ParseInt(q["limit"],  25);
            int    offset  = ParseInt(q["offset"],  0);

            if (_cacheReady)
            {
                var filtered = _allProductsList.Where(p =>
                {
                    if (!string.IsNullOrEmpty(srch))
                    {
                        string blob = Str(p,"description") + " " + Str(p,"product_name") + " " + Str(p,"size");
                        if (blob.IndexOf(srch, StringComparison.OrdinalIgnoreCase) < 0) return false;
                    }
                    if (!string.IsNullOrEmpty(mfr)     && Str(p,"manufacturer").IndexOf(mfr,     StringComparison.OrdinalIgnoreCase) < 0) return false;
                    if (!string.IsNullOrEmpty(mat)     && Str(p,"material").IndexOf(mat,          StringComparison.OrdinalIgnoreCase) < 0) return false;
                    if (!string.IsNullOrEmpty(install) && Str(p,"install_type").IndexOf(install,  StringComparison.OrdinalIgnoreCase) < 0) return false;
                    if (!string.IsNullOrEmpty(grp)     && Str(p,"group").IndexOf(grp,             StringComparison.OrdinalIgnoreCase) < 0) return false;
                    if (!string.IsNullOrEmpty(spec)    && Str(p,"specification").IndexOf(spec,    StringComparison.OrdinalIgnoreCase) < 0) return false;
                    if (!string.IsNullOrEmpty(size)    && Str(p,"size").IndexOf(size,             StringComparison.OrdinalIgnoreCase) < 0) return false;
                    return true;
                }).ToList();

                int total = filtered.Count;
                var page  = (limit <= 0 ? filtered : filtered.Skip(offset).Take(limit)).ToList();
                return Serialize(new Dict
                {
                    ["total"]       = total,
                    ["offset"]      = offset,
                    ["limit"]       = limit,
                    ["data"]        = page,
                    ["cache_ready"] = true,
                });
            }

            var results = new List<Dict>();
            SafeRead(() =>
            {
                int skipped = 0;
                foreach (var pd in ProductDatabase.ProductDefinitions)
                {
                    if (!string.IsNullOrEmpty(srch))
                    {
                        string blob = (pd.Description ?? "") + " " + (pd.ProductName ?? "") + " " + (pd.Size ?? "");
                        if (blob.IndexOf(srch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    if (!string.IsNullOrEmpty(mfr)     && (pd.Manufacturer    ?? "").IndexOf(mfr,     StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(mat)     && (pd.Material         ?? "").IndexOf(mat,     StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(install) && (pd.InstallType      ?? "").IndexOf(install, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(grp)     && (pd.Group?.Name      ?? "").IndexOf(grp,     StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(spec)    && (pd.Specification    ?? "").IndexOf(spec,    StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(size)    && (pd.Size             ?? "").IndexOf(size,    StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (skipped < offset) { skipped++; continue; }
                    if (limit > 0 && results.Count >= limit) break;
                    results.Add(ProductDefToDict(pd));
                }
            });
            return Serialize(new Dict { ["total"] = -1, ["offset"] = offset, ["limit"] = limit, ["data"] = results, ["cache_ready"] = false });
        }

        private string HandleProductCount(NameValueCollection q)
        {
            if (!_cacheReady) return Serialize(new Dict { ["count"] = -1, ["cache_ready"] = false });

            string srch    = (q["q"]            ?? "").ToLower();
            string mfr     = (q["manufacturer"] ?? "").ToLower();
            string mat     = (q["material"]      ?? "").ToLower();
            string install = (q["install_type"]  ?? "").ToLower();
            string grp     = (q["group"]         ?? "").ToLower();
            string spec    = (q["specification"] ?? "").ToLower();
            string size    = (q["size"]          ?? "").ToLower();

            int count = _allProductsList.Count(p =>
            {
                if (!string.IsNullOrEmpty(srch))
                {
                    string blob = Str(p,"description") + " " + Str(p,"product_name") + " " + Str(p,"size");
                    if (blob.IndexOf(srch, StringComparison.OrdinalIgnoreCase) < 0) return false;
                }
                if (!string.IsNullOrEmpty(mfr)     && Str(p,"manufacturer").IndexOf(mfr,     StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(mat)     && Str(p,"material").IndexOf(mat,          StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(install) && Str(p,"install_type").IndexOf(install,  StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(grp)     && Str(p,"group").IndexOf(grp,             StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(spec)    && Str(p,"specification").IndexOf(spec,    StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(size)    && Str(p,"size").IndexOf(size,             StringComparison.OrdinalIgnoreCase) < 0) return false;
                return true;
            });
            return Serialize(new Dict { ["count"] = count, ["cache_ready"] = true });
        }

        private string HandleGetProduct(string segment)
        {
            // Prefer cached product index (fast, thread-safe) over live API iteration
            if (_cacheReady && _productIndex.TryGetValue(segment, out var cached))
            {
                var result = new Dict(cached);
                result["prices"] = _priceCache.TryGetValue(segment, out var prices)
                    ? (object)prices : (object)new List<Dict>();
                result["install_times"] = _installCache.TryGetValue(segment, out var installs)
                    ? (object)installs : (object)new List<Dict>();
                return Serialize(result);
            }

            // Fallback to live API when cache isn't ready
            Dict liveResult = null;
            SafeRead(() =>
            {
                var pd = ProductDatabase.ProductDefinitions.FirstOrDefault(p => p.Id == segment);
                if (pd == null) return;
                liveResult = ProductDefToDict(pd, includeSupplierIds: true);
                liveResult["is_product_listed"] = _productListedIds.Contains(pd.Id) ? "Yes" : "No";
                liveResult["prices"] = _priceCache.TryGetValue(pd.Id, out var prices)
                    ? (object)prices : (object)new List<Dict>();
                liveResult["install_times"] = _installCache.TryGetValue(pd.Id, out var installs)
                    ? (object)installs : (object)new List<Dict>();
            });
            return liveResult != null ? Serialize(liveResult) : JsonError("product not found");
        }

        private string HandleGetServices()
        {
            var results = new List<Dict>();
            SafeRead(() =>
            {
                foreach (var svc in FabDB.Services)
                    results.Add(new Dict { ["name"] = FixEncoding(svc.Name ?? ""), ["template"] = FixEncoding(svc.ServiceTemplate?.Name ?? "") });
            });
            return Serialize(results);
        }

        private string HandleGetServiceItems(NameValueCollection q)
        {
            string svcFilter = (q["service"] ?? "").ToLower();
            int    limit     = ParseInt(q["limit"],  500);
            int    offset    = ParseInt(q["offset"],   0);

            if (!_cacheReady) return Serialize(new Dict { ["cache_ready"] = false, ["data"] = new List<Dict>(), ["total"] = -1 });

            var filtered = string.IsNullOrEmpty(svcFilter)
                ? _serviceItemsList
                : _serviceItemsList.Where(d => Str(d,"service_name").IndexOf(svcFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            int total = filtered.Count;
            var page  = (limit <= 0 ? filtered : filtered.Skip(offset).Take(limit)).ToList();
            return Serialize(new Dict { ["cache_ready"] = true, ["total"] = total, ["offset"] = offset, ["limit"] = limit, ["data"] = page });
        }

        /// <summary>
        /// Full price list entries: product_id, description, manufacturer, material, size,
        /// harrison_code, supplier_group, list_name, cost, discount_code, units, date, status.
        /// Supports filtering by supplier_group, list_name, or free-text search (q).
        /// </summary>
        private string HandlePriceListEntries(NameValueCollection q)
        {
            if (!_cacheReady) return Serialize(new Dict { ["cache_ready"] = false, ["data"] = new List<Dict>(), ["total"] = -1 });

            string sgFilter   = (q["supplier_group"] ?? "").ToLower();
            string listFilter = (q["list_name"]      ?? "").ToLower();
            string srch       = (q["q"]              ?? "").ToLower();
            int    limit      = ParseInt(q["limit"],  500);
            int    offset     = ParseInt(q["offset"],   0);

            var filtered = _priceEntriesList.Where(e =>
            {
                if (!string.IsNullOrEmpty(sgFilter)   && Str(e,"supplier_group").IndexOf(sgFilter,   StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(listFilter) && Str(e,"list_name").IndexOf(listFilter,       StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(srch))
                {
                    string blob = Str(e,"product_id") + " " + Str(e,"description") + " " + Str(e,"harrison_code");
                    if (blob.IndexOf(srch, StringComparison.OrdinalIgnoreCase) < 0) return false;
                }
                return true;
            }).ToList();

            int total = filtered.Count;
            var page  = (limit <= 0 ? filtered : filtered.Skip(offset).Take(limit)).ToList();
            return Serialize(new Dict { ["cache_ready"] = true, ["total"] = total, ["offset"] = offset, ["limit"] = limit, ["data"] = page });
        }

        /// <summary>
        /// Full install time entries: product_id, description, manufacturer, material, size,
        /// harrison_code, table_name, group, labor_rate, units, status.
        /// </summary>
        private string HandleInstallTimeEntries(NameValueCollection q)
        {
            if (!_cacheReady) return Serialize(new Dict { ["cache_ready"] = false, ["data"] = new List<Dict>(), ["total"] = -1 });

            string tableFilter = (q["table_name"]     ?? "").ToLower();
            string grpFilter   = (q["group"]          ?? "").ToLower();
            string srch        = (q["q"]              ?? "").ToLower();
            int    limit       = ParseInt(q["limit"],  500);
            int    offset      = ParseInt(q["offset"],   0);

            var filtered = _installEntriesList.Where(e =>
            {
                if (!string.IsNullOrEmpty(tableFilter) && Str(e,"table_name").IndexOf(tableFilter, StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(grpFilter)   && Str(e,"group").IndexOf(grpFilter,       StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(srch))
                {
                    string blob = Str(e,"product_id") + " " + Str(e,"description") + " " + Str(e,"harrison_code");
                    if (blob.IndexOf(srch, StringComparison.OrdinalIgnoreCase) < 0) return false;
                }
                return true;
            }).ToList();

            int total = filtered.Count;
            var page  = (limit <= 0 ? filtered : filtered.Skip(offset).Take(limit)).ToList();
            return Serialize(new Dict { ["cache_ready"] = true, ["total"] = total, ["offset"] = offset, ["limit"] = limit, ["data"] = page });
        }

        /// <summary>
        /// Returns base64-encoded PNG images keyed by product database ID.
        /// Reads PNG files captured during cache Phase 3 (image_path alongside .itm files).
        /// </summary>
        private string HandleImagesMap(NameValueCollection q)
        {
            if (!_cacheReady) return Serialize(new Dict { ["cache_ready"] = false, ["count"] = 0, ["images"] = new Dict() });

            int limit = ParseInt(q["limit"], 300);
            var images = new Dict();
            int count  = 0;

            foreach (var kvp in _productImageMap)
            {
                if (limit > 0 && count >= limit) break;
                if (!File.Exists(kvp.Value)) continue;
                try
                {
                    byte[] bytes = File.ReadAllBytes(kvp.Value);
                    images[kvp.Key] = "data:image/png;base64," + Convert.ToBase64String(bytes);
                    count++;
                }
                catch { }
            }
            return Serialize(new Dict { ["cache_ready"] = true, ["count"] = count, ["images"] = images });
        }

        private string HandleGetPriceLists(NameValueCollection q)
        {
            string supplierFilter = q["supplier_group"] ?? "";
            int    limit          = ParseInt(q["limit"], 100);
            var    results        = new List<Dict>();
            SafeRead(() =>
            {
                foreach (var sg in FabDB.SupplierGroups)
                {
                    if (results.Count >= limit) break;
                    if (!string.IsNullOrEmpty(supplierFilter) &&
                        (sg.Name ?? "").IndexOf(supplierFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    foreach (var list in sg.PriceLists)
                    {
                        if (results.Count >= limit) break;
                        if (list is PriceList pl)
                            results.Add(new Dict { ["supplier_group"] = sg.Name ?? "", ["list_name"] = pl.Name ?? "", ["entry_count"] = pl.Products.Count() });
                    }
                }
            });
            return Serialize(results);
        }

        private string HandleGetInstallTimes(NameValueCollection q)
        {
            int limit   = ParseInt(q["limit"], 100);
            var results = new List<Dict>();
            SafeRead(() =>
            {
                var tables = FabDB.InstallationTimesTable;
                if (tables == null) return;
                foreach (var table in tables)
                {
                    if (results.Count >= limit) break;
                    string tableType  = table is InstallationTimesTableWithBreakpoints ? "breakpoint" : "simple";
                    int    entryCount = table is InstallationTimesTable simple ? simple.Products.Count() : 0;
                    results.Add(new Dict { ["name"] = table.Name ?? "", ["group"] = table.Group ?? "", ["type"] = tableType, ["entry_count"] = entryCount });
                }
            });
            return Serialize(results);
        }

        // ── Materials / Sections / Specifications ────────────────────────────────

        private string HandleGetMaterials()
        {
            var results = new List<Dict>();
            SafeRead(() =>
            {
                try
                {
                    foreach (var mat in FabDB.Materials)
                    {
                        results.Add(new Dict
                        {
                            ["name"]   = mat.Name ?? "",
                            ["group"]  = mat.Group ?? "",
                            ["gauge_count"] = mat.Gauges?.Count() ?? 0,
                        });
                    }
                }
                catch { }
            });
            return Serialize(new Dict { ["count"] = results.Count, ["data"] = results });
        }

        private string HandleGetSections()
        {
            var results = new List<Dict>();
            SafeRead(() =>
            {
                try
                {
                    foreach (var sec in FabDB.Sections)
                    {
                        results.Add(new Dict
                        {
                            ["name"]  = sec.Description ?? "",
                            ["index"] = sec.Index,
                        });
                    }
                }
                catch { }
            });
            return Serialize(new Dict { ["count"] = results.Count, ["data"] = results });
        }

        private string HandleGetSpecifications()
        {
            var results = new List<Dict>();
            SafeRead(() =>
            {
                try
                {
                    foreach (var spec in FabDB.Specifications)
                    {
                        results.Add(new Dict
                        {
                            ["name"] = spec.Name ?? "",
                        });
                    }
                }
                catch { }
            });
            return Serialize(new Dict { ["count"] = results.Count, ["data"] = results });
        }

        // ── Job Items ─────────────────────────────────────────────────────────────

        private string HandleGetJobItems(NameValueCollection q)
        {
            if (!_cacheReady) return Serialize(new Dict { ["cache_ready"] = false, ["data"] = new List<Dict>(), ["total"] = 0 });

            string svcFilter  = (q["service"] ?? "").ToLower();
            string statFilter = (q["status"]  ?? "").ToLower();
            string secFilter  = (q["section"] ?? "").ToLower();
            string srch       = (q["q"]       ?? "").ToLower();
            int    limit      = ParseInt(q["limit"],  200);
            int    offset     = ParseInt(q["offset"],   0);

            var filtered = _jobItemsList.Where(d =>
            {
                if (!string.IsNullOrEmpty(svcFilter)  && Str(d,"service").IndexOf(svcFilter,  StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(statFilter)  && Str(d,"status").IndexOf(statFilter,  StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(secFilter)   && Str(d,"section").IndexOf(secFilter,   StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (!string.IsNullOrEmpty(srch))
                {
                    string blob = Str(d,"name") + " " + Str(d,"cid") + " " + Str(d,"unique_id");
                    if (blob.IndexOf(srch, StringComparison.OrdinalIgnoreCase) < 0) return false;
                }
                return true;
            }).ToList();

            int total = filtered.Count;
            var page  = (limit <= 0 ? filtered : filtered.Skip(offset).Take(limit)).ToList();
            return Serialize(new Dict { ["cache_ready"] = true, ["total"] = total, ["offset"] = offset, ["limit"] = limit, ["data"] = page });
        }

        private string HandleGetJobItem(string uniqueId)
        {
            if (!_cacheReady)
                return Serialize(new Dict { ["cache_ready"] = false, ["error"] = "cache not ready" });

            if (_jobItemIndex.TryGetValue(uniqueId, out var item))
                return SerializeDict(item);

            // Try case-insensitive lookup
            var match = _jobItemIndex.FirstOrDefault(kvp =>
                kvp.Key.Equals(uniqueId, StringComparison.OrdinalIgnoreCase));
            if (match.Value != null)
                return SerializeDict(match.Value);

            return JsonError("job item not found");
        }

        // ── Service Template Tree ─────────────────────────────────────────────────

        /// <summary>
        /// Build hierarchical tree: service → template → tabs → buttons → items → conditions.
        /// Uses the Phase 3 service items cache, grouped into a tree structure.
        /// Path: /api/service-templates/{name}/tree
        /// </summary>
        private string HandleGetServiceTemplateTree(string path)
        {
            // Extract service name from: /api/service-templates/{name}/tree
            string segment = path.Substring("/api/service-templates/".Length);
            segment = segment.Substring(0, segment.Length - "/tree".Length);
            string svcName = Uri.UnescapeDataString(segment);

            if (!_cacheReady)
                return Serialize(new Dict { ["cache_ready"] = false, ["error"] = "cache not ready" });

            // Find matching service items
            var svcItems = _serviceItemsList
                .Where(d => Str(d,"service_name").Equals(svcName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!svcItems.Any())
                return JsonError("service not found or has no items");

            string templateName = Str(svcItems[0], "template_name");

            // Group: button_name → items
            var buttonGroups = svcItems.GroupBy(d => Str(d, "button_name")).ToList();

            // Reconstruct tabs by looking at live service template structure
            // (Phase 3 doesn't store tab names, so build flat button list)
            var buttons = new List<Dict>();
            foreach (var bg in buttonGroups)
            {
                string btnName = bg.Key;
                var items = new List<Dict>();
                var entryNames = new HashSet<string>();

                foreach (var si in bg)
                {
                    string entryName = Str(si, "entry_name");
                    if (!string.IsNullOrEmpty(entryName))
                        entryNames.Add(entryName);

                    items.Add(new Dict
                    {
                        ["item_path"]       = Str(si, "item_path"),
                        ["image_path"]      = Str(si, "image_path"),
                        ["entry_name"]      = entryName,
                        ["condition_desc"]  = Str(si, "condition_desc"),
                        ["greater_than"]    = Str(si, "greater_than"),
                        ["less_than_eq"]    = Str(si, "less_than_eq"),
                        ["condition_id"]    = Str(si, "condition_id"),
                    });
                }

                // Deduplicate items by item_path + condition
                var uniqueItems = items
                    .GroupBy(i => Str(i,"item_path") + "|" + Str(i,"condition_id"))
                    .Select(g => g.First())
                    .ToList();

                // Find button image (use first non-empty button_image or image_path)
                string btnImage = bg.Select(si => Str(si,"button_image")).FirstOrDefault(s => !string.IsNullOrEmpty(s))
                    ?? bg.Select(si => Str(si,"image_path")).FirstOrDefault(s => !string.IsNullOrEmpty(s))
                    ?? "";

                buttons.Add(new Dict
                {
                    ["name"]           = btnName,
                    ["image"]          = btnImage,
                    ["item_count"]     = uniqueItems.Count,
                    ["product_ids"]    = string.Join(",", entryNames),
                    ["items"]          = uniqueItems,
                });
            }

            // Try to get tab structure from live API
            var tabs = new List<Dict>();
            SafeRead(() =>
            {
                try
                {
                    var svc = FabDB.Services.FirstOrDefault(s =>
                        (s.Name ?? "").Equals(svcName, StringComparison.OrdinalIgnoreCase));
                    if (svc?.ServiceTemplate?.ServiceTabs == null) return;

                    foreach (var tab in svc.ServiceTemplate.ServiceTabs)
                    {
                        string tabName = tab.Name ?? "";
                        var tabBtnNames = new HashSet<string>();
                        if (tab.ServiceButtons != null)
                            foreach (var btn in tab.ServiceButtons)
                                tabBtnNames.Add(btn.Name ?? "");

                        var tabButtons = buttons.Where(b => tabBtnNames.Contains(Str(b, "name"))).ToList();
                        tabs.Add(new Dict
                        {
                            ["name"]         = tabName,
                            ["button_count"] = tabButtons.Count,
                            ["buttons"]      = tabButtons,
                        });
                    }
                }
                catch { }
            });

            // If live API failed, return flat button list under a single "All" tab
            if (!tabs.Any())
            {
                tabs.Add(new Dict
                {
                    ["name"]         = "All",
                    ["button_count"] = buttons.Count,
                    ["buttons"]      = buttons,
                });
            }

            return Serialize(new Dict
            {
                ["cache_ready"]   = true,
                ["service_name"]  = svcName,
                ["template_name"] = templateName,
                ["tab_count"]     = tabs.Count,
                ["button_count"]  = buttons.Count,
                ["item_count"]    = svcItems.Count,
                ["tabs"]          = tabs,
            });
        }

        // ── Write Endpoints (POST/PUT) ────────────────────────────────────────────

        /// <summary>
        /// Add or update a price list entry.
        /// Body: { "product_id": "X", "supplier_group": "Y", "list_name": "Z", "cost": 1.23, ... }
        /// </summary>
        private string HandleAddPriceEntry(string body)
        {
            var data = ParseJsonBody(body);
            if (data == null) return JsonError("invalid JSON body");

            string productId     = StrBody(data, "product_id");
            string supplierGroup = StrBody(data, "supplier_group");
            string listName      = StrBody(data, "list_name");
            string costStr       = StrBody(data, "cost");

            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(supplierGroup) || string.IsNullOrEmpty(listName))
                return JsonError("product_id, supplier_group, and list_name are required");

            string error = null;
            SafeRead(() =>
            {
                try
                {
                    var sg = FabDB.SupplierGroups.FirstOrDefault(s =>
                        (s.Name ?? "").Equals(supplierGroup, StringComparison.OrdinalIgnoreCase));
                    if (sg == null) { error = "supplier group not found: " + supplierGroup; return; }

                    var pl = sg.PriceLists.OfType<PriceList>().FirstOrDefault(l =>
                        (l.Name ?? "").Equals(listName, StringComparison.OrdinalIgnoreCase));
                    if (pl == null) { error = "price list not found: " + listName; return; }

                    // Find existing entry or add new one
                    var existing = pl.Products.FirstOrDefault(e => e.DatabaseId == productId);
                    if (existing != null)
                    {
                        if (double.TryParse(costStr, out double cost))
                            existing.Value = cost;
                        string discountCode = StrBody(data, "discount_code");
                        if (!string.IsNullOrEmpty(discountCode))
                            existing.DiscountCode = discountCode;
                    }
                    else
                    {
                        var result = pl.AddEntry(productId);
                        if (result.Status == ResultStatus.Succeeded && result.ReturnObject is ProductEntry newEntry)
                        {
                            if (double.TryParse(costStr, out double cost))
                                newEntry.Value = cost;
                            string discountCode = StrBody(data, "discount_code");
                            if (!string.IsNullOrEmpty(discountCode))
                                newEntry.DiscountCode = discountCode;
                        }
                        else
                        {
                            error = "failed to add entry: " + (result.Message ?? "unknown error");
                        }
                    }
                }
                catch (Exception ex) { error = ex.Message; }
            });

            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = true, ["product_id"] = productId });
        }

        /// <summary>
        /// Add or update an install time entry.
        /// Body: { "product_id": "X", "table_name": "Y", "labor_rate": 0.45 }
        /// </summary>
        private string HandleAddInstallEntry(string body)
        {
            var data = ParseJsonBody(body);
            if (data == null) return JsonError("invalid JSON body");

            string productId = StrBody(data, "product_id");
            string tableName = StrBody(data, "table_name");
            string rateStr   = StrBody(data, "labor_rate");

            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(tableName))
                return JsonError("product_id and table_name are required");

            string error = null;
            SafeRead(() =>
            {
                try
                {
                    var tables = FabDB.InstallationTimesTable;
                    if (tables == null) { error = "no installation times tables"; return; }

                    var tbl = tables.OfType<InstallationTimesTable>().FirstOrDefault(t =>
                        (t.Name ?? "").Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    if (tbl == null) { error = "table not found: " + tableName; return; }

                    var existing = tbl.Products.FirstOrDefault(e => e.DatabaseId == productId);
                    if (existing != null)
                    {
                        if (double.TryParse(rateStr, out double rate))
                            existing.Value = rate;
                    }
                    else
                    {
                        var result = tbl.AddEntry(productId);
                        if (result.Status == ResultStatus.Succeeded && result.ReturnObject is ProductEntry newEntry)
                        {
                            if (double.TryParse(rateStr, out double rate))
                                newEntry.Value = rate;
                        }
                        else
                        {
                            error = "failed to add entry: " + (result.Message ?? "unknown error");
                        }
                    }
                }
                catch (Exception ex) { error = ex.Message; }
            });

            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = true, ["product_id"] = productId });
        }

        /// <summary>
        /// Update supplier IDs for a product.
        /// Path: /api/products/{id}/supplier-ids
        /// Body: { "Harrison": "ABC123", "Ferguson": "DEF456" }
        /// </summary>
        private string HandleUpdateSupplierIds(string path, string body)
        {
            // Extract product ID: /api/products/{id}/supplier-ids
            string segment = path.Substring("/api/products/".Length);
            segment = segment.Substring(0, segment.Length - "/supplier-ids".Length);
            string productId = Uri.UnescapeDataString(segment);

            var data = ParseJsonBody(body);
            if (data == null) return JsonError("invalid JSON body");

            string error = null;
            SafeRead(() =>
            {
                try
                {
                    var pd = ProductDatabase.ProductDefinitions.FirstOrDefault(p => p.Id == productId);
                    if (pd == null) { error = "product not found: " + productId; return; }

                    foreach (var kvp in data)
                    {
                        string supplierName = kvp.Key;
                        string newId = kvp.Value;

                        var sid = pd.SupplierIds.FirstOrDefault(s =>
                            (s.ProductSupplier?.Name ?? "").Equals(supplierName, StringComparison.OrdinalIgnoreCase));
                        if (sid != null)
                            sid.Id = newId;
                    }
                }
                catch (Exception ex) { error = ex.Message; }
            });

            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = true, ["product_id"] = productId });
        }

        // ── POST /api/products/harrison-codes/import ─────────────────────────────

        /// <summary>
        /// Bulk import Harrison codes for products.
        /// Body: TSV lines — product_id\tharrison_code (one per line).
        /// First line may be a header (pi_id/harrison_code) and is skipped.
        /// Returns summary with updated/not_found/error counts.
        /// </summary>
        private string HandleHarrisonImport(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return JsonError("empty body — send TSV lines: product_id\\tharrison_code");

            var lines = body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return JsonError("no data lines");

            // Skip header line if present
            int startIdx = 0;
            if (lines[0].Contains("pi_id") || lines[0].Contains("harrison_code")
                || lines[0].Contains("product_id"))
                startIdx = 1;

            int updated = 0, notFound = 0, noSupplier = 0, skipped = 0, errors = 0;
            var notFoundIds = new List<string>();
            var errorDetails = new List<string>();

            SafeRead(() =>
            {
                // Build a quick index for O(1) lookups
                var pdIndex = new Dictionary<string, ProductDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var pd in ProductDatabase.ProductDefinitions)
                {
                    if (!string.IsNullOrEmpty(pd.Id))
                        pdIndex[pd.Id] = pd;
                }

                for (int i = startIdx; i < lines.Length; i++)
                {
                    var parts = lines[i].Split('\t');
                    if (parts.Length < 2) { skipped++; continue; }

                    string productId    = parts[0].Trim();
                    string harrisonCode = parts[1].Trim();

                    if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(harrisonCode))
                    { skipped++; continue; }

                    try
                    {
                        if (!pdIndex.TryGetValue(productId, out var pd))
                        {
                            notFound++;
                            if (notFoundIds.Count < 20) notFoundIds.Add(productId);
                            continue;
                        }

                        var sid = pd.SupplierIds.FirstOrDefault(s =>
                            (s.ProductSupplier?.Name ?? "")
                            .Equals("Harrison", StringComparison.OrdinalIgnoreCase));

                        if (sid != null)
                        {
                            sid.Id = harrisonCode;
                            updated++;
                        }
                        else
                        {
                            noSupplier++;
                            if (errorDetails.Count < 10)
                                errorDetails.Add("No Harrison supplier slot: " + productId);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        if (errorDetails.Count < 10)
                            errorDetails.Add(productId + ": " + ex.Message);
                    }
                }
            });

            int total = lines.Length - startIdx;
            WriteMessage($"[FabBridge] Harrison import: {updated} updated, {notFound} not found, "
                       + $"{noSupplier} no supplier slot, {errors} errors, {skipped} skipped (of {total})");

            var result = new Dict
            {
                ["success"]     = updated > 0,
                ["total"]       = total,
                ["updated"]     = updated,
                ["not_found"]   = notFound,
                ["no_supplier"] = noSupplier,
                ["skipped"]     = skipped,
                ["errors"]      = errors,
            };
            if (notFoundIds.Count > 0)
                result["not_found_sample"] = string.Join(", ", notFoundIds);
            if (errorDetails.Count > 0)
                result["error_sample"] = string.Join("; ", errorDetails);

            return Serialize(result);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string Str(Dict d, string key) =>
            d.TryGetValue(key, out var v) ? (v?.ToString() ?? "") : "";

        private static Dict ProductDefToDict(ProductDefinition pd, bool includeSupplierIds = false)
        {
            string productGroup = "N/A";
            try { productGroup = pd.Group?.Name ?? "N/A"; } catch { }
            var d = new Dict
            {
                ["id"]           = pd.Id            ?? "",
                ["description"]  = pd.Description   ?? "",
                ["product_name"] = pd.ProductName   ?? "",
                ["size"]         = pd.Size           ?? "",
                ["manufacturer"] = pd.Manufacturer  ?? "",
                ["material"]     = pd.Material       ?? "",
                ["specification"]= pd.Specification  ?? "",
                ["install_type"] = pd.InstallType    ?? "",
                ["source"]       = pd.Source         ?? "",
                ["range"]        = pd.Range          ?? "",
                ["finish"]       = pd.Finish         ?? "",
                ["group"]        = productGroup,
            };
            if (includeSupplierIds)
            {
                var sids = new Dict();
                foreach (var sid in pd.SupplierIds)
                    sids[sid.ProductSupplier?.Name ?? "Unknown"] = sid.Id ?? "";
                d["supplier_ids"] = sids;
            }
            return d;
        }

        private static void SafeRead(Action action)
        {
            if (FabricationSample.ACADSample.IsShuttingDown) return;
            try { action(); }
            catch { }
        }

        private static void WriteMessage(string msg)
        {
            try { CADapp.DocumentManager?.MdiActiveDocument?.Editor?.WriteMessage($"\n{msg}"); }
            catch { }
        }

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, out int v) ? v : fallback;

        /// <summary>Read request body as UTF-8 string.</summary>
        private static string ReadBody(HttpListenerRequest req)
        {
            try
            {
                using (var reader = new System.IO.StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch { return ""; }
        }

        /// <summary>
        /// Minimal JSON body parser: extracts top-level string key-value pairs.
        /// Handles: { "key": "value", "key2": "value2", "key3": 123 }
        /// Returns null if body is empty or malformed.
        /// </summary>
        private static Dictionary<string, string> ParseJsonBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            body = body.Trim();
            if (!body.StartsWith("{") || !body.EndsWith("}")) return null;
            body = body.Substring(1, body.Length - 2); // strip { }

            var result = new Dictionary<string, string>();
            int pos = 0;
            while (pos < body.Length)
            {
                // Skip whitespace and commas
                while (pos < body.Length && (body[pos] == ' ' || body[pos] == ',' || body[pos] == '\n' || body[pos] == '\r' || body[pos] == '\t')) pos++;
                if (pos >= body.Length) break;

                // Read key (expect quoted string)
                if (body[pos] != '"') { pos++; continue; }
                pos++; // skip opening quote
                int keyStart = pos;
                while (pos < body.Length && body[pos] != '"') pos++;
                string key = body.Substring(keyStart, pos - keyStart);
                pos++; // skip closing quote

                // Skip colon
                while (pos < body.Length && body[pos] != ':') pos++;
                pos++; // skip colon

                // Skip whitespace
                while (pos < body.Length && (body[pos] == ' ' || body[pos] == '\t')) pos++;
                if (pos >= body.Length) break;

                // Read value (string, number, bool, null)
                string val;
                if (body[pos] == '"')
                {
                    pos++; // skip opening quote
                    var sb = new StringBuilder();
                    while (pos < body.Length && body[pos] != '"')
                    {
                        if (body[pos] == '\\' && pos + 1 < body.Length) { sb.Append(body[pos + 1]); pos += 2; }
                        else { sb.Append(body[pos]); pos++; }
                    }
                    val = sb.ToString();
                    pos++; // skip closing quote
                }
                else
                {
                    int valStart = pos;
                    while (pos < body.Length && body[pos] != ',' && body[pos] != '}' && body[pos] != ' ' && body[pos] != '\n') pos++;
                    val = body.Substring(valStart, pos - valStart).Trim();
                    if (val == "null") val = "";
                    if (val == "true") val = "true";
                    if (val == "false") val = "false";
                }

                result[key] = val;
            }
            return result.Count > 0 ? result : null;
        }

        private static string StrBody(Dictionary<string, string> d, string key) =>
            d != null && d.TryGetValue(key, out var v) ? v : "";

        // ── DELETE /api/price-lists/entries ─────────────────────────────────────
        // Query: list_name=MyList&product_id=ABC123&supplier_group=MySG
        private string HandleDeletePriceEntry(NameValueCollection query)
        {
            string listName  = query["list_name"]      ?? "";
            string productId = query["product_id"]     ?? "";
            string sgName    = query["supplier_group"] ?? "";
            if (string.IsNullOrEmpty(listName) || string.IsNullOrEmpty(productId))
                return JsonError("list_name and product_id are required");

            string error = null;
            SafeRead(() =>
            {
                try
                {
                    SupplierGroup sg = string.IsNullOrEmpty(sgName)
                        ? FabDB.SupplierGroups.FirstOrDefault()
                        : FabDB.SupplierGroups.FirstOrDefault(g =>
                            (g.Name ?? "").Equals(sgName, StringComparison.OrdinalIgnoreCase));
                    if (sg == null) { error = "supplier group not found"; return; }

                    var pl = sg.PriceLists.OfType<PriceList>().FirstOrDefault(p =>
                        (p.Name ?? "").Equals(listName, StringComparison.OrdinalIgnoreCase));
                    if (pl == null) { error = "price list not found: " + listName; return; }

                    var entry = pl.Products.FirstOrDefault(e => e.DatabaseId == productId);
                    if (entry == null) { error = "entry not found for product: " + productId; return; }

                    var result = pl.DeleteEntry(entry);
                    if (result.Status != ResultStatus.Succeeded)
                        error = result.Message ?? "delete failed";
                }
                catch (Exception ex) { error = ex.Message; }
            });
            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = true, ["product_id"] = productId });
        }

        // ── DELETE /api/install-times/entries ───────────────────────────────────
        // Query: table_name=MyTable&product_id=ABC123
        private string HandleDeleteInstallEntry(NameValueCollection query)
        {
            string tableName = query["table_name"] ?? "";
            string productId = query["product_id"] ?? "";
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(productId))
                return JsonError("table_name and product_id are required");

            string error = null;
            SafeRead(() =>
            {
                try
                {
                    var tbl = FabDB.InstallationTimesTable
                        .OfType<InstallationTimesTable>()
                        .FirstOrDefault(t => (t.Name ?? "").Equals(tableName, StringComparison.OrdinalIgnoreCase));
                    if (tbl == null) { error = "table not found: " + tableName; return; }

                    var entry = tbl.Products.FirstOrDefault(e => e.DatabaseId == productId);
                    if (entry == null) { error = "entry not found for product: " + productId; return; }

                    var result = tbl.DeleteEntry(entry);
                    if (result.Status != ResultStatus.Succeeded)
                        error = result.Message ?? "delete failed";
                }
                catch (Exception ex) { error = ex.Message; }
            });
            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = true, ["product_id"] = productId });
        }

        // ── POST /api/job/items/{uniqueId}/swap ─────────────────────────────────
        // Body: { "service_name": "...", "button_name": "...", "button_item_index": 0,
        //         "transfer_position": true, "transfer_dimensions": true, ... }
        private string HandleSwapItem(string path, string body)
        {
            string segment  = path.Substring("/api/job/items/".Length);
            string uniqueId = Uri.UnescapeDataString(segment.Substring(0, segment.Length - "/swap".Length));
            if (string.IsNullOrEmpty(uniqueId)) return JsonError("uniqueId required");

            var data = ParseJsonBody(body);
            if (data == null) return JsonError("invalid JSON body");

            string serviceName = StrBody(data, "service_name");
            string buttonName  = StrBody(data, "button_name");
            int buttonItemIndex = int.TryParse(StrBody(data, "button_item_index"), out int bi) ? bi : 0;
            if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(buttonName))
                return JsonError("service_name and button_name are required");

            string error = null;
            bool success = false;
            SafeRead(() =>
            {
                try
                {
                    Item originalItem = Job.Items.FirstOrDefault(i => i.UniqueId == uniqueId);
                    if (originalItem == null) { error = "item not found: " + uniqueId; return; }

                    Service svc = FabDB.Services.FirstOrDefault(s =>
                        (s.Name ?? "").Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                    if (svc == null) { error = "service not found: " + serviceName; return; }

                    ServiceButton btn = svc.ServiceTemplate?.ServiceTabs
                        .SelectMany(t => t.ServiceButtons)
                        .FirstOrDefault(b => (b.Name ?? "").Equals(buttonName, StringComparison.OrdinalIgnoreCase));
                    if (btn == null) { error = "button not found: " + buttonName; return; }

                    var options = new FabricationSample.Models.ItemSwapOptions
                    {
                        TransferPosition      = BoolBody(data, "transfer_position",      true),
                        TransferDimensions    = BoolBody(data, "transfer_dimensions",    true),
                        TransferOptions       = BoolBody(data, "transfer_options",       true),
                        TransferCustomData    = BoolBody(data, "transfer_custom_data",   true),
                        TransferBasicInfo     = BoolBody(data, "transfer_basic_info",    true),
                        TransferStatusSection = BoolBody(data, "transfer_status_section",true),
                        TransferPriceList     = BoolBody(data, "transfer_price_list",    false),
                    };

                    var swapSvc = new FabricationSample.Services.ItemSwap.ItemSwapService();
                    var result  = swapSvc.SwapItem(originalItem, btn, buttonItemIndex, options);
                    if (result.Success) success = true;
                    else error = result.ErrorMessage ?? "swap failed";
                }
                catch (Exception ex) { error = ex.Message; }
            });
            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = success });
        }

        // ── POST /api/job/items/undo ─────────────────────────────────────────────
        private string HandleUndoSwap()
        {
            string error = null;
            bool success = false;
            SafeRead(() =>
            {
                try
                {
                    var swapSvc = new FabricationSample.Services.ItemSwap.ItemSwapService();
                    var result  = swapSvc.UndoLastSwap();
                    if (result.Success) success = true;
                    else error = result.ErrorMessage ?? "undo failed";
                }
                catch (Exception ex) { error = ex.Message; }
            });
            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = success });
        }

        // ── POST /api/cache/export ───────────────────────────────────────────────
        // Body: { "type": "product_info" | "price_tables" | "install_times" | "item_data" }
        private string HandleCacheExport(string body)
        {
            var data = ParseJsonBody(body);
            string exportType = data != null ? StrBody(data, "type") : "";
            if (string.IsNullOrEmpty(exportType)) exportType = "product_info";

            string outputPath = null;
            string error      = null;
            SafeRead(() =>
            {
                try
                {
                    string folder    = System.IO.Path.GetTempPath();
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    switch (exportType.ToLowerInvariant())
                    {
                        case "product_info":
                            outputPath = System.IO.Path.Combine(folder, $"ProductInfo_{timestamp}.csv");
                            var r1 = new FabricationSample.Services.Export.ProductInfoExportService().Export(outputPath);
                            if (!r1.IsSuccess) error = r1.ErrorMessage;
                            break;
                        case "price_tables":
                            outputPath = System.IO.Path.Combine(folder, $"PriceTables_{timestamp}.csv");
                            var r2 = new FabricationSample.Services.Export.PriceTablesExportService().Export(outputPath);
                            if (!r2.IsSuccess) error = r2.ErrorMessage;
                            break;
                        case "install_times":
                            outputPath = System.IO.Path.Combine(folder, $"InstallationTimes_{timestamp}.csv");
                            var r3 = new FabricationSample.Services.Export.InstallationTimesExportService().Export(outputPath);
                            if (!r3.IsSuccess) error = r3.ErrorMessage;
                            break;
                        case "item_data":
                            outputPath = System.IO.Path.Combine(folder, $"ItemData_{timestamp}.csv");
                            var r4 = new FabricationSample.Services.Export.RevitBridgeExportService().Export(outputPath);
                            if (!r4.IsSuccess) error = r4.ErrorMessage;
                            break;
                        default:
                            error = "unknown export type: " + exportType;
                            break;
                    }
                }
                catch (Exception ex) { error = ex.Message; }
            });
            if (error != null) return JsonError(error);
            return Serialize(new Dict { ["success"] = true, ["type"] = exportType, ["path"] = outputPath ?? "" });
        }

        private static bool BoolBody(Dictionary<string, string> d, string key, bool defaultVal)
        {
            if (d != null && d.TryGetValue(key, out string s))
                return s.Equals("true", StringComparison.OrdinalIgnoreCase);
            return defaultVal;
        }

        // ── JSON serializer ───────────────────────────────────────────────────────

        private static string JsonError(string msg) => $"{{\"error\":\"{Esc(msg)}\"}}";

        private static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                     .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        /// <summary>
        /// Fix UTF-8 → Windows-1252 mojibake in Fabrication API strings.
        /// Uses explicit replacements for known mojibake sequences.
        /// </summary>
        private static string FixEncoding(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Common UTF-8 → Windows-1252 mojibake patterns:
            // Each 3-char sequence is a single Unicode char whose UTF-8 bytes
            // were misinterpreted as Windows-1252.
            return s
                .Replace("\u00e2\u20ac\u201c", "\u2013")   // en dash –
                .Replace("\u00e2\u20ac\u201d", "\u2014")   // em dash —
                .Replace("\u00e2\u20ac\u2122", "\u2019")   // right single quote '
                .Replace("\u00e2\u20ac\u02dc", "\u02dc")   // small tilde ˜
                .Replace("\u00e2\u20ac\u0153", "\u201c")   // left double quote "
                .Replace("\u00e2\u20ac\u00a2", "\u2022")   // bullet •
                .Replace("\u00c2\u00b0", "\u00b0");        // degree °
        }

        private static string Serialize(object val)
        {
            if (val == null) return "null";
            if (val is bool b)   return b ? "true" : "false";
            if (val is int i)    return i.ToString();
            if (val is long l)   return l.ToString();
            if (val is double db) return db.ToString("R");
            if (val is float f)  return f.ToString("R");
            if (val is string s) return $"\"{Esc(s)}\"";
            if (val is Dict d)   return SerializeDict(d);
            if (val is List<Dict> list) return SerializeList(list);
            if (val is HashSet<string> hs) return "[" + string.Join(",", hs.Select(x => $"\"{Esc(x)}\"")) + "]";
            return $"\"{Esc(val.ToString())}\"";
        }

        private static string SerializeDict(Dict d) =>
            "{" + string.Join(",", d.Select(kv => $"\"{kv.Key}\":{Serialize(kv.Value)}")) + "}";

        private static string SerializeList(List<Dict> list) =>
            "[" + string.Join(",", list.Select(SerializeDict)) + "]";

        private static string Serialize(Dict d)       => SerializeDict(d);
        private static string Serialize(List<Dict> l) => SerializeList(l);

        private class Dict : Dictionary<string, object>
        {
            public Dict() : base() { }
            public Dict(IDictionary<string, object> other) : base(other) { }
        }
    }
}
