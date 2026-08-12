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

        const confirmationPage = await response.blob();
        location.replace(URL.createObjectURL(confirmationPage));
    } catch {
        submitButton.disabled = false;
        let error = document.getElementById("shutdown-error");
        if (error === null) {
            error = document.createElement("p");
            error.id = "shutdown-error";
            error.setAttribute("role", "alert");
            form.append(error);
        }

        error.textContent = "Nie udało się zamknąć Servandy. Spróbuj ponownie.";
    }
});
