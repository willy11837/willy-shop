// site extras for login modal and ajax add-to-cart
document.addEventListener('DOMContentLoaded', function() {
    // click handler for checkout buttons: if not authenticated, open login modal
    document.querySelectorAll('.js-checkout-btn').forEach(function(el) {
        el.addEventListener('click', function(e) {
            var href = el.getAttribute('data-href') || el.getAttribute('href');
            if (window.isAuthenticated !== 'true') {
                e.preventDefault();
                if (typeof window.openLoginModal === 'function') {
                    window.openLoginModal(href || '/Checkout/Index');
                }
            }
        });
    });

    // AJAX add-to-cart
    document.querySelectorAll('.js-add-cart').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            var code = btn.getAttribute('data-code');
            // send AJAX POST to Details AddToCart
            fetch('/Products/Details?handler=AddToCart&code=' + encodeURIComponent(code) + '&qty=1', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            }).then(function(res) {
                if (res.ok) {
                    // simple feedback
                    btn.textContent = '已加入';
                    setTimeout(function() { btn.textContent = '加入購物車'; }, 1500);
                } else if (res.status === 401) {
                    // not authenticated - open login modal for checkout flow
                    window.openLoginModal(window.location.pathname);
                } else {
                    btn.textContent = '錯誤';
                }
            }).catch(function() {
                btn.textContent = '錯誤';
            });
        });
    });
});
