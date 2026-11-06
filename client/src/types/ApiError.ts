// Shape of errors thrown by axios / our API services, as read in catch blocks.
export interface ApiError {
    message?: string;
    errorCode?: string;
    response?: {
        status?: number;
        data?: {
            message?: string;
            error?: { message?: string };
        };
    };
}
