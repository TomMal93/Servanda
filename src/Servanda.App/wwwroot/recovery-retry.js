const retryForm = document.getElementById("recovery-retry");
document.documentElement.dataset.recoveryRetryReady = retryForm ? "true" : "false";

document.addEventListener("change", event => {
    if (event.target instanceof HTMLInputElement && event.target.id === "confirm-recovery-restore") {
        syncRestoreButton();
    }
});

document.addEventListener("keydown", event => {
    const actionButton = findActionButton(event.target);
    if (actionButton && event.key === "Enter") {
        event.preventDefault();
        actionButton.click();
    }
});

document.addEventListener("click", async event => {
    const actionButton = findActionButton(event.target);
    const currentForm = actionButton?.closest("form");
    if (!actionButton || !currentForm) {
        return;
    }

    if (currentForm.id === "recovery-restore"
        && !document.getElementById("confirm-recovery-restore")?.checked) {
        return;
    }

    actionButton.setAttribute("disabled", "");
    const failureId = currentForm.id === "recovery-restore"
        ? "recovery-restore-failure"
        : "recovery-retry-failure";

    try {
        const response = await fetch(currentForm.action, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Accept": "application/json" },
            body: new FormData(currentForm)
        });

        if (!response.ok) {
            throw new Error("recovery action rejected");
        }

        const result = await response.json();
        if (typeof result.redirectTo !== "string" || !result.redirectTo.startsWith("/")) {
            throw new Error("recovery action returned an invalid destination");
        }

        if (result.redirectTo !== "/") {
            showFailure(failureId, actionButton);
            return;
        }

        window.location.replace(result.redirectTo);
    } catch {
        showFailure(failureId, actionButton);
    }
});

window.addEventListener("pageshow", syncRestoreButton);
syncRestoreButton();

function findActionButton(target) {
    return target instanceof Element
        ? target.closest("#recovery-retry button[type='button'], #recovery-restore button[type='button']")
        : null;
}

function syncRestoreButton() {
    const confirmation = document.getElementById("confirm-recovery-restore");
    const restoreButton = document.querySelector("#recovery-restore button[type='button']");
    if (confirmation instanceof HTMLInputElement && restoreButton instanceof HTMLButtonElement) {
        restoreButton.disabled = !confirmation.checked;
    }
}

function showFailure(failureId, actionButton) {
    const failureMessage = document.getElementById(failureId);
    if (failureMessage) {
        failureMessage.hidden = false;
    }

    actionButton?.removeAttribute("disabled");
}
