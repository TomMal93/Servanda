let targetId = null;

function isTextEntry(element) {
    if (!element) {
        return false;
    }

    const name = element.tagName;
    return name === "INPUT" || name === "TEXTAREA" || name === "SELECT" || element.isContentEditable;
}

function onKeyDown(event) {
    if (!event.ctrlKey || event.metaKey || event.altKey || event.key.toLowerCase() !== "k") {
        return;
    }

    if (isTextEntry(document.activeElement)) {
        return;
    }

    const field = targetId ? document.getElementById(targetId) : null;
    if (field) {
        event.preventDefault();
        field.focus();
        field.select?.();
    }
}

export function register(id) {
    if (targetId === null) {
        document.addEventListener("keydown", onKeyDown);
    }

    targetId = id;
}

export function unregister() {
    document.removeEventListener("keydown", onKeyDown);
    targetId = null;
}
