export function initializePwa(blazor) {
    if (!('serviceWorker' in navigator) || !window.isSecureContext) return;

    const dismissedKey = 'ruvents-update-dismissed';
    let registration;
    let installPrompt;
    let dismissed = false;
    let reloadRequested = false;
    let installMessage = '';
    const standalone = matchMedia('(display-mode: standalone)');

    try {
        dismissed = sessionStorage.getItem(dismissedKey) === '1';
    } catch {
        dismissed = false;
    }

    const setDismissed = value => {
        dismissed = value;
        try {
            if (value) sessionStorage.setItem(dismissedKey, '1');
            else sessionStorage.removeItem(dismissedKey);
        } catch {
            // Storage can be unavailable; the choice still applies for this page.
        }
    };

    // An update exists only when a worker is waiting while an older worker still
    // controls this page. On a first visit nothing controls the page yet, so the
    // banner must stay hidden instead of announcing a "new version" of nothing.
    const hasWaitingUpdate = () => Boolean(navigator.serviceWorker.controller) && Boolean(registration?.waiting);

    const render = () => {
        const update = document.getElementById('pwa-update');
        if (update) update.hidden = !hasWaitingUpdate() || dismissed;
        const help = document.querySelector('[data-pwa-install-help]');
        if (help) help.hidden = standalone.matches || navigator.standalone === true;
        const action = document.querySelector('[data-pwa-install-action]');
        if (action) action.hidden = !installPrompt;
        const message = document.querySelector('[data-pwa-install-message]');
        if (message) message.textContent = installMessage;
    };

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        installPrompt = event;
        render();
    });
    window.addEventListener('appinstalled', () => {
        installPrompt = undefined;
        installMessage = 'Ruvents is installed.';
        render();
    });
    standalone.addEventListener('change', render);
    blazor.addEventListener('enhancedload', render);

    document.addEventListener('click', async event => {
        const target = event.target instanceof Element ? event.target : null;
        if (target?.closest('[data-pwa-install]') && installPrompt) {
            const prompt = installPrompt;
            installPrompt = undefined;
            render();
            try {
                await prompt.prompt();
                await prompt.userChoice;
            } catch {
                installMessage = 'Use your browser’s menu to install Ruvents.';
            }
            render();
        }
        if (target?.closest('[data-pwa-later]')) {
            setDismissed(true);
            render();
        }
        if (target?.closest('[data-pwa-reload]') && !reloadRequested) {
            if (!window.confirm('Reload Ruvents now? Finish any save and copy any unsaved work first. This page will reload.')) return;
            reloadRequested = true;
            setDismissed(true);
            render();
            if (registration?.waiting) {
                registration.waiting.postMessage({ type: 'ACTIVATE_UPDATE' });
                // Activating is cooperative; reload anyway if the worker never claims
                // this page so the user is not left on a stale document.
                window.setTimeout(() => window.location.reload(), 4000);
            } else {
                window.location.reload();
            }
        }
    });

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        // Only the tab whose user requested the update reloads. Other tabs may
        // contain unsaved forms or an uncertain write and must stay intact.
        if (reloadRequested) {
            window.location.reload();
            return;
        }

        // The waiting worker claimed this page, so any pending update is now active.
        setDismissed(false);
        render();
    });

    // The URL stays stable across deployments. Revalidate the generated worker
    // (including its content-hash version) rather than reuse HTTP cache.
    navigator.serviceWorker.register(new URL('service-worker.js', document.baseURI), { updateViaCache: 'none' })
        .then(worker => {
            registration = worker;
            const notifyWhenInstalled = event => {
                if (event.currentTarget.state === 'installed') {
                    // A new update replaces any earlier "Later" choice.
                    setDismissed(false);
                    render();
                }
            };
            const observeInstalling = () => worker.installing?.addEventListener('statechange', notifyWhenInstalled);
            worker.addEventListener('updatefound', observeInstalling);
            // A navigation check or another tab may have started installation
            // before register() resolved and before this updatefound listener.
            observeInstalling();
            render();
        })
        .catch(() => {
            // Browsing remains usable if storage is disabled or registration fails.
        });

    let lastCheck = Date.now();
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible' || Date.now() - lastCheck < 60_000) return;
        lastCheck = Date.now();
        registration?.update().catch(() => { /* Recheck on a later visit when connected. */ });
    });
    render();
}
