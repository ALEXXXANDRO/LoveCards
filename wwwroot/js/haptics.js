(function () {
    window.appHaptics = {
        vibrate(ms) {
            try {
                if (navigator.vibrate) navigator.vibrate(ms);
            }
            catch { }
        }
    };
})();
