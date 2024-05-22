import MouseTooltip from "./MouseTooltip.svelte";

export class TooltipCreatorResult {
    element: HTMLElement;
    updateFunction: (position: MouseEvent, text: string) => void;
    dispose: () => void;
}
export class TooltipCreator {
    public static CreateTooltip(label: string, event: MouseEvent): TooltipCreatorResult {
        const element = document.createElement("mouse-tooltip") as HTMLElement;
        const content = document.getElementsByClassName("content");
        content[0].appendChild(element);
        element.setAttribute("value", "5505");
        element.setAttribute("label", label);
        element.setAttribute("left", event.clientX - 20 + "");
        element.setAttribute("top", event.clientY - 40 + "");
        return {
            element: element,
            updateFunction: (position: MouseEvent, text: string) => {
                element.setAttribute("left", position.clientX - 20 + "");
                element.setAttribute("top", position.clientY - 40 + "");
                element.setAttribute("value", text);
            },
            dispose: () => {
                content[0].removeChild(element);
            }
        };
    }
}