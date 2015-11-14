using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Utilz
{
    public class PointerOverTrigger : StateTriggerBase
    {
        private FrameworkElement _targetElement;
        public FrameworkElement TargetElement
        {
            get
            {
                return _targetElement;
            }
            set
            {
                RemoveHandlers();
                _targetElement = value;
                AddHandlers();
            }
        }

        private bool _isHandlersActive = false;

        private void AddHandlers()
        {
            if (!_isHandlersActive && _targetElement != null)
            {
                _targetElement.PointerEntered += OnTargetElement_PointerEntered;
                _targetElement.PointerExited += OnTargetElement_PointerExited;
                _isHandlersActive = true;
            }
        }

        private void RemoveHandlers()
        {
            if (_targetElement != null)
            {
                _targetElement.PointerEntered -= OnTargetElement_PointerEntered;
                _targetElement.PointerExited -= OnTargetElement_PointerExited;
                _isHandlersActive = false;
            }
        }

        private void OnTargetElement_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SetActive(false);
        }

        private void OnTargetElement_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SetActive(true);
        }
    }
}
