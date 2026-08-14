const form = document.getElementById("recovery-retry");
document.documentElement.dataset.recoveryRetryReady = form ? "true" : "false";

document.addEventListener("keydown", event => {
    const retryButton = findRetryButton(event.target);
    if (retryButton && event.key === "Enter") {
        event.preventDefault();
        retryButton.click();
    }
});

document.addEventListener("click", async event => {
    const retryButton = findRetryButton(event.target);
    const currentForm = retryButton?.closest("form");
    if (!retryButton || !currentForm) {
        return;
    }

    retryButton.setAttribute("disabled", "");

    try {
        const response = await fetch(currentForm.action, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Accept": "application/json" },
            body: new FormData(currentForm)
        });

        if (!response.ok) {
            throw new Error("recovery retry rejected");
        }

        const result = await response.json();
        if (typeof result.redirectTo !== "string" || !result.redirectTo.startsWith("/")) {
            throw new Error("recovery retry returned an invalid destination");
        }

        if (result.redirectTo === "/recovery?retry=failed") {
            showFailure(retryButton);
            return;
        }

        window.location.replace(result.redirectTo);
    } catch {
        showFailure(retryButton);
    }
});

function findRetryButton(target) {
    return target instanceof Element
        ? target.closest("#recovery-retry button[type='button']")
        : null;
}

function showFailure(submitButton) {
    const currentFailureMessage = document.getElementById("recovery-retry-failure");
    if (currentFailureMessage) {
        currentFailureMessage.hidden = false;
    }

    submitButton?.removeAttribute("disabled");
}
