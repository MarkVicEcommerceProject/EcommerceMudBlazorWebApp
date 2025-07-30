window.cartStorage = {
    getGuestId: function () {
        return localStorage.getItem("guestCartId")
    },
    setGuestId: function (guestId) {
        localStorage.setItem("guestCartId", guestId)
    },
    clearGuestId: function () {
        localStorage.removeItem("guestCartId")
    }
}