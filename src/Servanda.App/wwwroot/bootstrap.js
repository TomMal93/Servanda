const statusElement = document.getElementById("bootstrap-status");
const fragment = new URLSearchParams(window.location.hash.slice(1));
const ticket = fragment.get("ticket");

history.replaceState(null, "", "/bootstrap");

if (!ticket) {
    statusElement.textContent = "Brak biletu startowego. Otwórz Servandę ponownie z menu aplikacji.";
} else {
    try {
        const response = await fetch("/session/bootstrap", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ ticket })
        });

        if (!response.ok) {
            throw new Error("bootstrap rejected");
        }

        window.location.replace("/");
    } catch {
        statusElement.textContent = "Nie udało się potwierdzić sesji. Otwórz Servandę ponownie z menu aplikacji.";
    }
}
