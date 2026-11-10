using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Warehouse_25dh230017
{
    public partial class Product
    {
        public string PriceLevel
        {
            get
            {
                if (Price < 100000) return "Giá thấp";
                if (Price < 500000) return "Giá trung bình";
                return "Giá cao";
            }
        }
    }
}