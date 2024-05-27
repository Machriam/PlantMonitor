// eslint-disable-next-line @typescript-eslint/no-unused-vars
import MouseTooltip from "./MouseTooltip.svelte";

export class TooltipCreatorResult {
    element: HTMLElement;
    updateFunction: (position: MouseEvent, text: string) => void;
    dispose: () => void;
}
export class TooltipCreator {

    public static CreateTooltip(label: string, event: MouseEvent): TooltipCreatorResult {
        let scrollY = window.scrollY;
        const onScroll = () => {
            const scrollDiff = window.scrollY - scrollY;
            scrollY = window.scrollY;
            const oldTop = parseInt(element.getAttribute("top") ?? "0");
            element.setAttribute("top", oldTop + scrollDiff + "");
        }
        const element = document.createElement("mouse-tooltip") as HTMLElement;
        const content = document.getElementsByClassName("content");
        window.addEventListener("scroll", onScroll);
        content[0].appendChild(element);
        element.setAttribute("value", "NA");
        element.setAttribute("label", label);
        element.setAttribute("left", event.clientX - 20 + "");
        element.setAttribute("top", event.clientY - 40 + "");
        return {
            element: element,
            updateFunction: (position: MouseEvent, text: string) => {
                element.setAttribute("left", position.clientX - 20 + "");
                element.setAttribute("top", position.clientY - 40 + scrollY + "");
                element.setAttribute("value", text);
            },
            dispose: () => {
                window.removeEventListener("scroll", onScroll)
                content[0].removeChild(element);
            }
        };
    }
}