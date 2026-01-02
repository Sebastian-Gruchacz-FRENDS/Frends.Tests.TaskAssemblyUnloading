using System.Reflection;

namespace Frends.Tests.TaskAssemblyUnloading
{
    public class LibraryLoader
    {

    }

    public class TaskRunner
    {
        private readonly MethodInfo _taskMethod;

        public TaskRunner(MethodInfo taskMethod)
        {
            _taskMethod = taskMethod;

            ValidateTaskMethod();
        }

        /// <summary>
        /// Tries to run Task using provided parameters
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// /// <param name="parameters"></param>
        /// <returns></returns>
        public Task RunTaskAsync(CancellationToken cancellationToken, params object?[] parameters)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Tries to run Task with defaulting params using default constructors
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <remarks>Might crash, if no default constructor is available</remarks>
        public Task RunTaskAsync(CancellationToken cancellationToken)
        {
            var parameters = new List<object?>();
            foreach (var pInfo in _taskMethod.GetParameters())
            {
                var pType = pInfo.ParameterType;
                object? value = Activator.CreateInstance(pType);
                parameters.Add(value);
            }

            return RunTaskAsync(cancellationToken, parameters.ToArray());
        }

        private void ValidateTaskMethod()
        {
            if (!_taskMethod.IsStatic)
            {
                throw new ArgumentException("Task method must be static.");
            }

            if (!_taskMethod.IsPublic)
            {
                throw new ArgumentException("Task method must be public.");
            }

            // TODO: more validation? asynchronous?
        }
    }
}
