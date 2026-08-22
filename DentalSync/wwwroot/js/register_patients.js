(function () {
    var modal = document.getElementById('createPatientModal');
    var openButton = document.getElementById('openCreatePatient');
    if (!modal || !openButton) return;

    function closeModal() {
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    }

    function openModal() {
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        var firstInput = document.getElementById('createFirstName');
        if (firstInput) {
            firstInput.focus();
        }
    }

    openButton.addEventListener('click', openModal);

    modal.querySelectorAll('[data-close-patient-modal]').forEach(function (button) {
        button.addEventListener('click', closeModal);
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && modal.classList.contains('is-open')) {
            closeModal();
        }
    });

    // Auto-open modal if there was a server validation error when submitting
    var hasError = modal.querySelector('.modal-error');
    if (hasError) {
        openModal();
    }
})();
