using System;

namespace SalesManagerApp.Models
{
    [Serializable]
    public class SaleTransaction : EntityBase
    {
        private int _productId;
        private string _productName;
        private int _customerId;
        private string _customerName;
        private int _quantity;
        private decimal _unitPrice;
        private DateTime _date;

        public SaleTransaction()
        {
            _productName = string.Empty;
            _customerName = string.Empty;
            _date = DateTime.Today;
        }

        public SaleTransaction(int id, int productId, string productName, int customerId, string customerName, int quantity, decimal unitPrice, DateTime date)
        {
            Id = id;
            _productId = productId;
            _productName = productName;
            _customerId = customerId;
            _customerName = customerName;
            _quantity = quantity;
            _unitPrice = unitPrice;
            _date = date;
        }

        public int ProductId
        {
            get { return _productId; }
            set { _productId = value; }
        }

        public string ProductName
        {
            get { return _productName; }
            set { _productName = value == null ? string.Empty : value.Trim(); }
        }

        public int CustomerId
        {
            get { return _customerId; }
            set { _customerId = value; }
        }

        public string CustomerName
        {
            get { return _customerName; }
            set { _customerName = value == null ? string.Empty : value.Trim(); }
        }

        public int Quantity
        {
            get { return _quantity; }
            set { _quantity = value; }
        }

        public decimal UnitPrice
        {
            get { return _unitPrice; }
            set { _unitPrice = value; }
        }

        public DateTime Date
        {
            get { return _date; }
            set { _date = value; }
        }

        public decimal Total
        {
            get { return Quantity * UnitPrice; }
        }

        public override string Validate()
        {
            if (ProductId <= 0)
            {
                return "Selectati un produs.";
            }

            if (CustomerId <= 0)
            {
                return "Selectati un client.";
            }

            if (Quantity <= 0)
            {
                return "Cantitatea trebuie sa fie mai mare decat 0.";
            }

            if (UnitPrice <= 0)
            {
                return "Pretul unitar trebuie sa fie mai mare decat 0.";
            }

            return string.Empty;
        }

        public decimal GetTransactionValue()
        {
            return Total;
        }

        public static explicit operator decimal(SaleTransaction transaction)
        {
            return transaction.Total;
        }
    }
}
