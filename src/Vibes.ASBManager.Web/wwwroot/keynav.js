// Arrow-key navigation for the message dialog. Invokes the .NET Navigate(direction)
// method on Left/Up (previous) and Right/Down (next) — but stays out of the way while
// the user is editing, so arrows keep their normal meaning inside inputs/textareas.
let handler = null;

export function register(dotnet) {
    unregister();
    handler = (e) => {
        if (e.altKey || e.ctrlKey || e.metaKey) return;
        let dir;
        if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') dir = -1;
        else if (e.key === 'ArrowRight' || e.key === 'ArrowDown') dir = 1;
        else return;

        const el = document.activeElement;
        const tag = el && el.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (el && el.isContentEditable)) return;

        e.preventDefault();
        dotnet.invokeMethodAsync('Navigate', dir);
    };
    document.addEventListener('keydown', handler);
}

export function unregister() {
    if (handler) {
        document.removeEventListener('keydown', handler);
        handler = null;
    }
}
