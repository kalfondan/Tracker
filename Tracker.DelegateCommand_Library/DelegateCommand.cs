using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Tracker.DelegateCommand_Library
{

    public sealed class DelegateCommand : ICommand
    {
        private readonly Func<object, bool> m_canExecute;
        private readonly Action<object> m_execute;
        private Action sendData;

        /// <summary>
        /// Initializes new instamce of <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="executeHandler">Handler which executes the command.</param>
        public DelegateCommand(Action<object> executeHandler)
        {
            m_execute = executeHandler;
        }

        /// <summary>
        /// Initializes new instamce of <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="canExecuteHandler">Handler which determines if the command can be executed in current state.</param>
        /// <param name="executeHandler">Handler which executes the command.</param>
        public DelegateCommand(Func<object, bool> canExecuteHandler, Action<object> executeHandler)
        {
            m_canExecute = canExecuteHandler;
            m_execute = executeHandler;
        }

        public DelegateCommand(Action sendData)
        {
            this.sendData = sendData;
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <returns>
        /// true if this command can be executed; otherwise, false.
        /// </returns>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        public bool CanExecute(object parameter)
        {
            return m_canExecute == null || m_canExecute(parameter);
        }

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        public void Execute(object parameter)
        {
            if (m_execute != null) m_execute(parameter);
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Raises an event <see cref="ICommand.CanExecuteChanged"/> which tells to a binded object that <see cref="ICommand.CanExecute"/> state is changed and need to be required.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            EventHandler handler = CanExecuteChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

    }
}
