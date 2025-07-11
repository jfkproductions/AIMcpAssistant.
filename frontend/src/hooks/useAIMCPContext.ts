import { useState, useCallback } from 'react';

interface AIMCPContextResult {
  handled: boolean;
  message: string;
}

interface UseAIMCPContextReturn {
  currentMCPContext: string | null;
  handleAIMCPCommand: (input: string) => AIMCPContextResult;
  getCurrentMCPContext: () => string | undefined;
  setCurrentMCPContext: (context: string | null) => void;
}

export const useAIMCPContext = (): UseAIMCPContextReturn => {
  const [currentMCPContext, setCurrentMCPContext] = useState<string | null>(null);

  const handleAIMCPCommand = useCallback((input: string): AIMCPContextResult => {
    const trimmedInput = input.trim();
    const aimcpRegex = /^AIMCP\s+(\w+)\s+(start|end)$/i;
    const match = trimmedInput.match(aimcpRegex);
    
    if (!match) {
      return { handled: false, message: '' };
    }
    
    const [, mcpName, action] = match;
    const normalizedMcpName = mcpName.toLowerCase();
    
    // Map common MCP names to their actual IDs
    const mcpMapping: { [key: string]: string } = {
      'mail': 'email',
      'email': 'email',
      'calendar': 'calendar',
      'chat': 'chatgpt',
      'chatgpt': 'chatgpt'
    };
    
    const mcpId = mcpMapping[normalizedMcpName] || normalizedMcpName;
    
    if (action.toLowerCase() === 'start') {
      setCurrentMCPContext(mcpId);
      return {
        handled: true,
        message: `✅ Entered ${mcpName.toUpperCase()} context. All subsequent commands will be routed to the ${mcpName} module until you run 'AIMCP ${mcpName} end'.`
      };
    } else if (action.toLowerCase() === 'end') {
      if (currentMCPContext === mcpId) {
        setCurrentMCPContext(null);
        return {
          handled: true,
          message: `✅ Exited ${mcpName.toUpperCase()} context. Commands will now be routed automatically based on intent.`
        };
      } else if (currentMCPContext === null) {
        return {
          handled: true,
          message: `ℹ️ No active ${mcpName.toUpperCase()} context to exit.`
        };
      } else {
        return {
          handled: true,
          message: `⚠️ Currently in ${currentMCPContext?.toUpperCase()} context. Use 'AIMCP ${currentMCPContext} end' to exit first.`
        };
      }
    }
    
    return { handled: false, message: '' };
  }, [currentMCPContext]);
  
  const getCurrentMCPContext = useCallback((): string | undefined => {
    return currentMCPContext || undefined;
  }, [currentMCPContext]);

  return {
    currentMCPContext,
    handleAIMCPCommand,
    getCurrentMCPContext,
    setCurrentMCPContext
  };
};