using System;
using System.Threading;

namespace Utilz
{
    public sealed class SemaphoreSlimSafeRelease : SemaphoreSlim
    {
        private volatile bool _isDisposed = false;
        public bool IsDisposed { get { return _isDisposed; } }

        public SemaphoreSlimSafeRelease(int initialCount) : base(initialCount) { }
        public SemaphoreSlimSafeRelease(int initialCount, int maxCount) : base(initialCount, maxCount) { }

        //new public int Release()
        //{
        //    try
        //    {
        //        return base.Release();
        //    }
        //    catch (ObjectDisposedException) { return -1; } // fires when I dispose sema and have not rector'd it while the current thread is inside it
        //    catch (SemaphoreFullException) { return -2; } // fires when I dispose sema and rector it while the current thread is inside it
        //    catch (Exception) { return -3; }
        //}

        //new public int Release(int releaseCount)
        //{
        //    try
        //    {
        //        return base.Release(releaseCount);
        //    }
        //    catch (ObjectDisposedException) { return -1; } // fires when I dispose sema and have not rector'd it while the current thread is inside it
        //    catch (SemaphoreFullException) { return -2; } // fires when I dispose sema and rector it while the current thread is inside it
        //    catch (Exception) { return -3; }
        //}

        //new public void Dispose()
        //{
        //    if (!_isDisposed)
        //    {
        //        try
        //        {
        //            base.Dispose();
        //            _isDisposed = true;
        //        }
        //        catch (ObjectDisposedException) { } // fires when I dispose sema and have not rector'd it while the current thread is inside it
        //        catch (SemaphoreFullException) { } // fires when I dispose sema and rector it while the current thread is inside it
        //        catch (Exception) { }
        //    }
        //}

        protected override void Dispose(bool disposing) // LOLLO TODO put all these things into the hiking mate. 
            // Do not forget to use the new Try... methods!
            // For traditional semaphores, make extension methods.
        {
            //if (!_isDisposed)
            //{
            try
            {
                base.Dispose(disposing);
            }
            catch (ObjectDisposedException) { } // fires when I dispose sema and have not rector'd it while the current thread is inside it
            catch (SemaphoreFullException) { } // fires when I dispose sema and rector it while the current thread is inside it
            catch (Exception) { }
            finally
            {
                _isDisposed = true;
            }
            //}            
        }
        /// <summary>
        /// Returns true if a SemaphoreSlimSafeRelease is not null and not disposed.
        /// </summary>
        /// <param name="semaphore"></param>
        /// <returns></returns>
        public static bool IsAlive(SemaphoreSlimSafeRelease semaphore)
        {
            return semaphore != null && !semaphore._isDisposed;
        }
        public static bool TryRelease(SemaphoreSlimSafeRelease semaphore)
        {
            try
            {
                if (IsAlive(semaphore))
                {
                    semaphore.Release();
                    return true;
                }
            }
            catch { }
            return false;
        }
        public static bool TryRelease(SemaphoreSlimSafeRelease semaphore, int releaseCount)
        {
            try
            {
                if (IsAlive(semaphore))
                {
                    semaphore.Release(releaseCount);
                    return true;
                }
            }
            catch { }
            return false;
        }
        public static bool TryDispose(SemaphoreSlimSafeRelease semaphore)
        {
            try
            {
                if (IsAlive(semaphore))
                {
                    semaphore.Dispose();
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
