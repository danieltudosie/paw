using System;

namespace SalesManagerApp.Models
{
    [Serializable]
    public class Customer : EntityBase, ICloneable, IComparable<Customer>
    {
        private string _fullName;
        private string _email;
        private string _phone;
        private int _loyaltyPoints;

        public Customer()
        {
            _fullName = string.Empty;
            _email = string.Empty;
            _phone = string.Empty;
        }

        public Customer(int id, string fullName, string email, string phone, int loyaltyPoints)
        {
            Id = id;
            _fullName = fullName;
            _email = email;
            _phone = phone;
            _loyaltyPoints = loyaltyPoints;
        }

        public string FullName
        {
            get { return _fullName; }
            set { _fullName = value == null ? string.Empty : value.Trim(); }
        }

        public string Email
        {
            get { return _email; }
            set { _email = value == null ? string.Empty : value.Trim(); }
        }

        public string Phone
        {
            get { return _phone; }
            set { _phone = value == null ? string.Empty : value.Trim(); }
        }

        public int LoyaltyPoints
        {
            get { return _loyaltyPoints; }
            set { _loyaltyPoints = value; }
        }

        public override string Validate()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                return "Numele clientului este obligatoriu.";
            }

            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
            {
                return "Email invalid.";
            }

            if (string.IsNullOrWhiteSpace(Phone))
            {
                return "Telefonul clientului este obligatoriu.";
            }

            return string.Empty;
        }

        public bool HasLoyaltyDiscount()
        {
            return LoyaltyPoints >= 10;
        }

        public object Clone()
        {
            return new Customer(Id, FullName, Email, Phone, LoyaltyPoints);
        }

        public int CompareTo(Customer other)
        {
            if (other == null)
            {
                return 1;
            }

            return LoyaltyPoints.CompareTo(other.LoyaltyPoints);
        }

        public override string ToString()
        {
            return FullName;
        }
    }
}
