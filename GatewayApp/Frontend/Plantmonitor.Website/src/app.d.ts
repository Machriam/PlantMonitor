// See https://kit.svelte.dev/docs/types#app
// for information about these interfaces
declare global {
	interface Window {
		cv: typeof import('mirada/dist/src/types/opencv/_types');
	}
	namespace App {
		interface Error { }
		// interface Locals {}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}
	}
}
export { };
