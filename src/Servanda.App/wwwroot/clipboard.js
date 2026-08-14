export async function copy(text) {
    if (!navigator.clipboard || !window.isSecureContext) {
        return false;
    }

    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}
