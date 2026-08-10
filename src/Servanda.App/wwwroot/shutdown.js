document.addEventListener("submit", async event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || form.id !== "shutdown-confirmation") {
        return;
    }

    event.preventDefault();

    const submitButton = form.querySelector("button[type='submit']");
    submitButton.disabled = true;

    try {
        const response = await fetch(form.action, {
            method: "POST",
            credentials: "same-origin",
            body: new FormData(form)
        });

        if (!response.ok) {
            throw new Error("shutdown rejected");
        }

        const message = await response.text();
        const main = document.createElement("main");
        const heading = document.createElement("h1");
        heading.textContent = "Servanda została zamknięta";
        const description = document.createElement("p");
        description.textContent = message;
        main.append(heading, description);
        document.body.replaceChildren(main);
        document.title = "Servanda została zamknięta";
        history.replaceState(null, "", "/shutdown");
    } catch {
        submitButton.disabled = false;
        const warning = document.getElementById("shutdown-warning");
        warning.textContent = "Nie udało się zamknąć Servandy. Spróbuj ponownie.";
    }
});
