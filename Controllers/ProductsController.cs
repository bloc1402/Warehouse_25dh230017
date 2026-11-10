using System;
using System.Linq;
using System.Web.Mvc;
using Warehouse_25dh230017;

namespace Warehouse_25dh230017.Controllers
{
    public class ProductsController : Controller
    {
        Warehouse_25dh230017Entities db = new Warehouse_25dh230017Entities();

        // ===== CÂU 2a + CÂU 3: danh sách, lọc, sắp xếp =====
        public ActionResult Index(decimal? fromPrice, decimal? toPrice, string category, string sort)
        {
            var q = db.Products.AsQueryable();

            if (fromPrice.HasValue && toPrice.HasValue && fromPrice > toPrice)
            {
                ViewBag.Error = "Khoảng giá không hợp lệ: Từ giá không được lớn hơn Đến giá.";
            }
            else
            {
                if (fromPrice.HasValue) q = q.Where(p => p.Price >= fromPrice.Value);
                if (toPrice.HasValue) q = q.Where(p => p.Price <= toPrice.Value);
                if (!string.IsNullOrEmpty(category)) q = q.Where(p => p.Category == category);
            }

            if (sort == "asc") q = q.OrderBy(p => p.Price);
            else if (sort == "desc") q = q.OrderByDescending(p => p.Price);
            else q = q.OrderBy(p => p.ProductID);

            ViewBag.Categories = new SelectList(db.Products.Select(p => p.Category).Distinct().ToList(), category);
            ViewBag.FromPrice = fromPrice;
            ViewBag.ToPrice = toPrice;
            ViewBag.Sort = sort;
            return View(q.ToList());
        }

        // ===== CÂU 2b: chỉnh sửa =====
        public ActionResult Edit(int id)
        {
            var p = db.Products.Find(id);
            if (p == null) return HttpNotFound();
            return View(p);
        }

        [HttpPost]
        public ActionResult Edit(int ProductID, string ProductName, string Category, decimal? Price)
        {
            var p = db.Products.Find(ProductID);
            if (p == null) return HttpNotFound();

            // giữ giá trị người dùng nhập để hiển thị lại
            string name = (ProductName ?? "").Trim();
            string cat = (Category ?? "").Trim();
            bool ok = true;

            if (name == "") { ModelState.AddModelError("", "Tên sản phẩm không được rỗng."); ok = false; }
            if (cat == "") { ModelState.AddModelError("", "Danh mục không được rỗng."); ok = false; }
            if (!Price.HasValue || Price.Value <= 0) { ModelState.AddModelError("", "Giá phải lớn hơn 0."); ok = false; }

            if (!ok)
            {
                ViewBag.Name = ProductName; ViewBag.Cat = Category; ViewBag.Pr = Price;
                return View(p);   // p vẫn là dữ liệu cũ trong CSDL, chưa bị đổi
            }

            // chỉ sửa 3 trường; ProductID và Quantity giữ nguyên
            p.ProductName = name;
            p.Category = cat;
            p.Price = Price.Value;
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // ===== CÂU 4: tăng/giảm giá theo % =====
        public ActionResult Adjust(int? id)
        {
            ViewBag.Products = new SelectList(db.Products.ToList(), "ProductID", "ProductName", id);
            return View();
        }

        [HttpPost]
        public ActionResult Adjust(int id, string mode, decimal? percent)
        {
            var p = db.Products.Find(id);
            ViewBag.Products = new SelectList(db.Products.ToList(), "ProductID", "ProductName", id);
            if (p == null) { ViewBag.Error = "Không tìm thấy sản phẩm."; return View(); }

            if (!percent.HasValue) { ViewBag.Error = "Vui lòng nhập tỷ lệ phần trăm."; }
            else if (mode == "increase")
            {
                if (percent <= 0 || percent > 100)
                    ViewBag.Error = "Tỷ lệ tăng phải > 0 và <= 100%.";
                else
                {
                    p.Price = Math.Round(p.Price * (1 + percent.Value / 100m), 2);
                    db.SaveChanges();
                    ViewBag.Message = "Tăng giá thành công.";
                }
            }
            else // decrease
            {
                if (percent <= 0 || percent >= 100)
                    ViewBag.Error = "Tỷ lệ giảm phải > 0 và < 100%.";
                else
                {
                    decimal newPrice = Math.Round(p.Price * (1 - percent.Value / 100m), 2);
                    if (newPrice < 1000)
                        ViewBag.Error = "Giá sau giảm (" + newPrice.ToString("N0") + ") nhỏ hơn 1.000 đồng, không được lưu.";
                    else
                    {
                        p.Price = newPrice;
                        db.SaveChanges();
                        ViewBag.Message = "Giảm giá thành công.";
                    }
                }
            }
            // đọc lại từ CSDL để hiển thị giá + mức giá hiện tại
            db.Entry(p).Reload();
            return View(p);
        }

        // ===== CÂU 5: giảm giá 2 sản phẩm trong 1 transaction =====
        public ActionResult DiscountTwo()
        {
            ViewBag.Products1 = new SelectList(db.Products.ToList(), "ProductID", "ProductName");
            ViewBag.Products2 = new SelectList(db.Products.ToList(), "ProductID", "ProductName");
            return View();
        }

        [HttpPost]
        public ActionResult DiscountTwo(int id1, int id2, decimal? percent1, decimal? percent2)
        {
            ViewBag.Products1 = new SelectList(db.Products.ToList(), "ProductID", "ProductName", id1);
            ViewBag.Products2 = new SelectList(db.Products.ToList(), "ProductID", "ProductName", id2);

            if (id1 == id2) { ViewBag.Error = "Phải chọn hai sản phẩm khác nhau."; return View(); }
            if (!percent1.HasValue || !percent2.HasValue) { ViewBag.Error = "Vui lòng nhập đủ hai tỷ lệ giảm."; return View(); }
            if (percent1 <= 0 || percent1 >= 100 || percent2 <= 0 || percent2 >= 100)
            { ViewBag.Error = "Mỗi tỷ lệ giảm phải > 0 và < 100%."; return View(); }

            using (var tran = db.Database.BeginTransaction())
            {
                try
                {
                    var a = db.Products.Find(id1);
                    var b = db.Products.Find(id2);
                    if (a == null || b == null) throw new Exception("Không tìm thấy sản phẩm.");

                    decimal newA = Math.Round(a.Price * (1 - percent1.Value / 100m), 2);
                    decimal newB = Math.Round(b.Price * (1 - percent2.Value / 100m), 2);

                    if (newA < 1000 || newB < 1000)
                        throw new Exception("Có giá sau giảm nhỏ hơn 1.000 đồng (A: " + newA.ToString("N0") +
                                            ", B: " + newB.ToString("N0") + "). Từ chối toàn bộ, không sản phẩm nào bị đổi giá.");

                    a.Price = newA;
                    b.Price = newB;
                    db.SaveChanges();
                    tran.Commit();
                    ViewBag.Message = "Thành công: " + a.ProductName + " = " + newA.ToString("N0") +
                                      ", " + b.ProductName + " = " + newB.ToString("N0");
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    ViewBag.Error = ex.Message;
                }
            }
            return View();
        }

        // ===== CÂU 6: thêm sản phẩm =====
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(string ProductName, string Category, decimal? Price, int? Quantity)
        {
            bool ok = true;
            if (string.IsNullOrWhiteSpace(ProductName)) { ModelState.AddModelError("", "Tên sản phẩm không được để trống."); ok = false; }
            if (string.IsNullOrWhiteSpace(Category)) { ModelState.AddModelError("", "Danh mục không được để trống."); ok = false; }
            if (!Price.HasValue || Price.Value <= 0) { ModelState.AddModelError("", "Giá phải lớn hơn 0."); ok = false; }
            if (!Quantity.HasValue || Quantity.Value < 0) { ModelState.AddModelError("", "Số lượng phải là số nguyên không âm."); ok = false; }

            if (!ok)
            {
                ViewBag.Name = ProductName; ViewBag.Cat = Category; ViewBag.Pr = Price; ViewBag.Qty = Quantity;
                return View();
            }

            db.Products.Add(new Product
            {
                ProductName = ProductName.Trim(),
                Category = Category.Trim(),
                Price = Price.Value,
                Quantity = Quantity.Value
            });
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // ===== CÂU 7: thống kê =====
        public ActionResult Stats()
        {
            ViewBag.Total = db.Products.Count();
            ViewBag.HighCount = db.Products.Count(p => p.Price >= 500000);
            ViewBag.TotalValue = db.Products.Sum(p => (decimal?)(p.Price * p.Quantity)) ?? 0m;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}