let enabled = false;

function warnBeforeUnload(event) {
    event.preventDefault();
    event.returnValue = "";
}

export function enable() {
    if (!enabled) {
        window.addEventListener("beforeunload", warnBeforeUnload);
        enabled = true;
    }
}

export function disable() {
    if (enabled) {
        window.removeEventListener("beforeunload", warnBeforeUnload);
        enabled = false;
    }
}
