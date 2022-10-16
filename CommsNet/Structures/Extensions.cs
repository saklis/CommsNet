using System.Reflection;
using System.Threading.Tasks;

namespace CommsNet.Structures
{
    public static class Extensions
    {
        /// <summary>
        ///     Executed method asynchronously and returns its result.
        /// </summary>
        /// <param name="this">       MethodInfo object. </param>
        /// <param name="obj">        Instance on which method will be executed. </param>
        /// <param name="parameters"> Method's parameters. </param>
        /// <returns> Result. </returns>
        public static async Task<object> InvokeAsync(this MethodInfo @this, object obj, params object[] parameters)
        {
            dynamic awaitable = @this.Invoke(obj, parameters);
            await awaitable;
            return awaitable.GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Executed method asynchronously and returns its result.
        /// </summary>
        /// <param name="this">       MethodInfo object. </param>
        /// <param name="obj">        Instance on which method will be executed. </param>
        /// <param name="parameters"> Method's parameters. </param>
        public static async Task InvokeVoidAsync(this MethodInfo @this, object obj, params object[] parameters)
        {
            dynamic awaitable = @this.Invoke(obj, parameters);
            await awaitable;
            awaitable.GetAwaiter().GetResult();
        }
    }
}