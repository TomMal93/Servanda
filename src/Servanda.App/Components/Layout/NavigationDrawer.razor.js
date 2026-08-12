export function open(dialog) {
    if (dialog.dataset.focusTrapInitialized !== "true") {
        dialog.addEventListener("keydown", event => trapFocus(dialog, event));
        dialog.dataset.focusTrapInitialized = "true";
    }

    if (!dialog.open) {
        dialog.showModal();
    }
}

export function close(dialog, trigger) {
    if (dialog.open) {
        dialog.close();
    }

    trigger.focus();
}

function trapFocus(dialog, event) {
    if (event.key !== "Tab") {
        return;
    }

    const focusableElements = [...dialog.querySelectorAll(
        "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])")]
        .filter(element => element.getClientRects().length > 0);

    if (focusableElements.length === 0) {
        event.preventDefault();
        return;
    }

    const first = focusableElements[0];
    const last = focusableElements[focusableElements.length - 1];
    if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
    }
}
