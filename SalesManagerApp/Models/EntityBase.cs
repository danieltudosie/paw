namespace SalesManagerApp.Models
{
    public abstract class EntityBase
    {
        public int Id { get; set; }

        public abstract string Validate();
    }
}
