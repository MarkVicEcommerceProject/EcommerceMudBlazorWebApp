namespace ECommerceMudblazorWebApp.Services
{
    public class DebounceDispatcher
    {
        private CancellationTokenSource _cts = new();

        public async Task DebounceAsync(Func<Task> action,int millisecondsDelay)
        {
            _cts.Cancel();
            _cts.Dispose();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                await Task.Delay(millisecondsDelay,token);
                if (!token.IsCancellationRequested)
                    await action();
            }
            catch (TaskCanceledException) 
            {

            }
        }
    }
}
