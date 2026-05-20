/// <reference types="react-scripts" />

declare global {
	interface Window {
		__APP_REGION__?: string;
		__APP_CURRENCY__?: string;
	}
}

export {};
