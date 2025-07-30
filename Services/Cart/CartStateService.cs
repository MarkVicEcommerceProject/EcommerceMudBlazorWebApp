namespace ECommerceMudblazorWebApp.Services.Cart
{
    public class CartStateService
    {
        private int _cartCount;

        public event Func<Task>? OnChange;

        public int CartCount
        {
            get => _cartCount;
            set
            {
                if (_cartCount != value)
                {
                    _cartCount = value;
                    _ = NotifyStateChangedAsync(); // Fire and forget
                }
            }
        }

        public async Task NotifyStateChangedAsync()
        {
            if (OnChange != null)
            {
                try
                {
                    await OnChange.Invoke();
                }
                catch (Exception ex)
                {
                    // Optionally log the error
                    Console.Error.WriteLine($"CartState OnChange error: {ex.Message}");
                }
            }
        }
    }

}


