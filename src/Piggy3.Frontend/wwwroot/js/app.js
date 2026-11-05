(function () {
    function absoluteCents(input) {
        if (typeof input !== 'string') return null;

        var text = input.trim()
            .replace(/€/g, '')
            .replace(/\s/g, '')
            .replace(/\u00a0/g, '');

        if (text.indexOf(',') !== -1 && text.indexOf('.') !== -1) return null;

        text = text.replace(',', '.');
        if ((text.match(/\./g) || []).length > 1) return null;

        var digits = text.replace(/^[+-]/, '');
        if (digits.charAt(0) === '.' || digits.charAt(digits.length - 1) === '.') return null;

        var value = Number.parseFloat(text);
        if (!Number.isFinite(value)) return null;

        return Math.round(Math.abs(value) * 100);
    }

    document.addEventListener('DOMContentLoaded', function () {
        var valueInput = document.getElementById('Value');
        var accountSelect = document.getElementById('AccountId');
        var dateInput = document.getElementById('Date');
        var costsElement = document.getElementById('monthly-costs');

        if (!valueInput) return;

        valueInput.focus();

        var costs = [];
        if (costsElement) {
            try {
                costs = JSON.parse(costsElement.textContent) || [];
            } catch (e) {
                costs = [];
            }
        }

        if (accountSelect) {
            valueInput.addEventListener('input', function () {
                var cents = absoluteCents(valueInput.value);
                if (cents === null) return;

                var matches = costs.filter(function (cost) {
                    return Math.round(Math.abs(Number(cost.value)) * 100) === cents;
                });

                if (matches.length === 1) {
                    accountSelect.value = matches[0].accountId;
                }
            });
        }

        if (dateInput) {
            dateInput.addEventListener('keydown', function (event) {
                if (event.key === 'Tab' && !event.shiftKey) {
                    event.preventDefault();
                    valueInput.focus();
                }
            });
        }
    });
})();
