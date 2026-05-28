using System;
using System.Collections;
using System.Collections.Generic;

namespace SalesManagerApp.Models
{
    [Serializable]
    public class Product : EntityBase, ICloneable, IComparable<Product>, IEnumerable<int>
    {
        private string _name;
        private string _category;
        private decimal _price;
        private int _stock;
        private int[] _monthlySold;

        public Product()
        {
            _name = string.Empty;
            _category = string.Empty;
            _monthlySold = new int[12];
        }

        public Product(int id, string name, string category, decimal price, int stock)
        {
            Id = id;
            _name = name;
            _category = category;
            _price = price;
            _stock = stock;
            _monthlySold = new int[12];
        }

        public string Name
        {
            get { return _name; }
            set { _name = value == null ? string.Empty : value.Trim(); }
        }

        public string Category
        {
            get { return _category; }
            set { _category = value == null ? string.Empty : value.Trim(); }
        }

        public decimal Price
        {
            get { return _price; }
            set { _price = value; }
        }

        public int Stock
        {
            get { return _stock; }
            set { _stock = value; }
        }

        public int this[int monthIndex]
        {
            get
            {
                if (monthIndex < 0 || monthIndex > 11)
                {
                    throw new IndexOutOfRangeException("Indexul lunii trebuie sa fie intre 0 si 11.");
                }

                return _monthlySold[monthIndex];
            }
            set
            {
                if (monthIndex < 0 || monthIndex > 11)
                {
                    throw new IndexOutOfRangeException("Indexul lunii trebuie sa fie intre 0 si 11.");
                }

                _monthlySold[monthIndex] = value;
            }
        }

        public override string Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "Numele produsului este obligatoriu.";
            }

            if (Price <= 0)
            {
                return "Pretul trebuie sa fie mai mare decat 0.";
            }

            if (Stock < 0)
            {
                return "Stocul nu poate fi negativ.";
            }

            return string.Empty;
        }

        public int GetTotalSold()
        {
            int total = 0;
            foreach (int value in _monthlySold)
            {
                total += value;
            }

            return total;
        }

        public decimal GetStockValue()
        {
            return Price * Stock;
        }

        public object Clone()
        {
            Product copy = new Product(Id, Name, Category, Price, Stock);

            for (int i = 0; i < _monthlySold.Length; i++)
            {
                copy[i] = _monthlySold[i];
            }

            return copy;
        }

        public int CompareTo(Product other)
        {
            if (other == null)
            {
                return 1;
            }

            return Price.CompareTo(other.Price);
        }

        public static Product operator +(Product product, int quantity)
        {
            Product copy = (Product)product.Clone();
            copy.Stock += quantity;
            return copy;
        }

        public static Product operator -(Product product, int quantity)
        {
            Product copy = (Product)product.Clone();
            copy.Stock = Math.Max(0, copy.Stock - quantity);
            return copy;
        }

        public static bool operator >(Product left, Product right)
        {
            return left.Price > right.Price;
        }

        public static bool operator <(Product left, Product right)
        {
            return left.Price < right.Price;
        }

        public IEnumerator<int> GetEnumerator()
        {
            return ((IEnumerable<int>)_monthlySold).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
