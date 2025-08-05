window.initSlider = () => {
    console.log('initSlider called');
    const slider = document.getElementById("slider");
    const prevBtn = document.getElementById("prev-btn");
    const nextBtn = document.getElementById("next-btn");

    if (!slider || !prevBtn || !nextBtn) {
        console.warn("Slider or buttons not found");
        return;
    }

    const card = slider.querySelector('.snap-center');
    const cardWidth = card ? card.offsetWidth + 16 : 288; // default width

    prevBtn.addEventListener("click", () => {
        slider.scrollBy({ left: -cardWidth, behavior: "smooth" });
    });

    nextBtn.addEventListener("click", () => {
        slider.scrollBy({ left: cardWidth, behavior: "smooth" });
    });
};
